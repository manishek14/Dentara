namespace Dentara.Infrastructure.Persistence;

using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Common;
using Dentara.Domain.Exceptions.Payments;
using Dentara.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

/// <summary>
/// SQL Server-backed implementation of IIdempotencyService. Uses the
/// IdempotencyRecords table per ADR-006.
///
/// Flow:
///   1. Try INSERT IdempotencyRecord with Status=InProgress.
///      If UNIQUE(Scope, Key) violation → record exists. Read it:
///        - If Status=Completed: deserialize ResponsePayload and return.
///        - If Status=InProgress: throw IdempotencyConflictException.
///        - If Status=Failed: retry the operation.
///   2. Execute the operation.
///   3. UPDATE the record to Completed or Failed with ResponsePayload.
/// </summary>
public sealed class SqlIdempotencyService : IIdempotencyService
{
    private readonly IDbContextAccessor _dbContextAccessor;
    private readonly IClock _clock;
    private readonly ILogger<SqlIdempotencyService> _logger;

    public SqlIdempotencyService(
        IDbContextAccessor dbContextAccessor,
        IClock clock,
        ILogger<SqlIdempotencyService> logger)
    {
        _dbContextAccessor = dbContextAccessor;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<TResponse>> ExecuteAsync<TResponse>(
        string scope,
        string key,
        Func<CancellationToken, Task<Result<TResponse>>> operation,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null)
    {
        var dbContext = _dbContextAccessor.Context;

        // 1. Check for an existing record
        var existing = await dbContext.IdempotencyRecords
            .Where(r => r.Scope == scope && r.Key == key)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing != null)
        {
            // Clean up expired records — allow retry
            if (existing.IsExpired(_clock))
            {
                _logger.LogInformation(
                    "Idempotency record {Scope}/{Key} expired; removing for retry", scope, key);
                dbContext.IdempotencyRecords.Remove(existing);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            else
            {
                return await HandleExistingRecordAsync<TResponse>(existing, cancellationToken);
            }
        }

        // 2. Create a new InProgress record
        var record = IdempotencyRecord.Create(scope, key, _clock, ttl: ttl);
        await dbContext.IdempotencyRecords.AddAsync(record, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Race condition: another thread inserted the record first.
            _logger.LogWarning(ex,
                "Idempotency race condition for {Scope}/{Key}; re-reading", scope, key);
            existing = await dbContext.IdempotencyRecords
                .Where(r => r.Scope == scope && r.Key == key)
                .FirstAsync(cancellationToken);
            return await HandleExistingRecordAsync<TResponse>(existing, cancellationToken);
        }

        // 3. Execute the operation
        Result<TResponse> result;
        try
        {
            result = await operation(cancellationToken);
        }
        catch (Exception ex)
        {
            record.MarkFailed(ex.Message, _clock);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        // 4. Update the record with the result
        var payloadJson = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = false
        });
        record.MarkCompleted(payloadJson, null, _clock);
        await dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    private async Task<Result<TResponse>> HandleExistingRecordAsync<TResponse>(
        IdempotencyRecord existing,
        CancellationToken cancellationToken)
    {
        if (existing.Status == IdempotencyStatus.Completed)
        {
            _logger.LogInformation(
                "Replaying completed idempotent operation {Scope}/{Key}",
                existing.Scope, existing.Key);
            var payload = existing.ResponsePayload;
            if (string.IsNullOrEmpty(payload))
                return Result<TResponse>.Failure(
                    "IDEMPOTENCY_REPLAY_FAILED",
                    "Completed record has no response payload.");
            return JsonSerializer.Deserialize<Result<TResponse>>(payload, new JsonSerializerOptions())
                ?? Result<TResponse>.Failure(
                    "IDEMPOTENCY_REPLAY_FAILED",
                    "Failed to deserialize replayed response.");
        }
        if (existing.Status == IdempotencyStatus.InProgress)
        {
            throw new IdempotencyConflictException(existing.Scope, existing.Key, "InProgress");
        }
        // Failed — allow retry by removing the record and letting the caller re-invoke.
        _logger.LogInformation(
            "Retrying failed idempotent operation {Scope}/{Key}", existing.Scope, existing.Key);
        _dbContextAccessor.Context.IdempotencyRecords.Remove(existing);
        await _dbContextAccessor.Context.SaveChangesAsync(cancellationToken);
        return Result<TResponse>.Failure(
            "IDEMPOTENCY_PREVIOUS_FAILED",
            "Previous operation failed; caller should retry.");
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        // SQL Server unique constraint error number is 2627 or 2601
        return ex.InnerException is Microsoft.Data.SqlClient.SqlException sqlEx
            && (sqlEx.Number == 2627 || sqlEx.Number == 2601);
    }
}
