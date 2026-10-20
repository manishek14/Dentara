namespace Dentara.Infrastructure.BackgroundJobs;

using Dentara.Domain.Common;
using Dentara.Domain.Payments;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Background worker that periodically cleans up old IdempotencyRecords.
///
/// Per ADR-006: idempotency records accumulate over time and must be cleaned
/// up to control DB size. This worker runs daily and removes records older
/// than 90 days (configurable). Only Completed or Failed records are removed
/// — InProgress records are preserved to avoid race conditions with ongoing
/// operations.
/// </summary>
public sealed class IdempotencyCleanupWorker : WorkerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly TimeSpan _retentionPeriod;

    public IdempotencyCleanupWorker(
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<IdempotencyCleanupWorker> logger,
        TimeSpan? retentionPeriod = null)
        : base(TimeSpan.FromHours(24), logger, errorDelay: TimeSpan.FromHours(1))
    {
        _serviceProvider = serviceProvider;
        _clock = clock;
        _retentionPeriod = retentionPeriod ?? TimeSpan.FromDays(90);
    }

    public override async Task ExecuteIterationAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DentaraDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<IdempotencyCleanupWorker>>();

        var cutoff = _clock.UtcNow - _retentionPeriod;

        // Find old records that are NOT InProgress (preserve ongoing operations)
        var oldRecords = await db.IdempotencyRecords
            .Where(r => r.CreatedAtUtc < cutoff
                && r.StatusValue != IdempotencyStatus.InProgress.Value)
            .Take(1000) // batch to avoid locking the table too long
            .ToListAsync(stoppingToken);

        if (oldRecords.Count == 0)
            return;

        db.IdempotencyRecords.RemoveRange(oldRecords);
        var deleted = await db.SaveChangesAsync(stoppingToken);

        logger.LogInformation(
            "IdempotencyCleanup: deleted {Count} records older than {Cutoff}",
            deleted, cutoff);
    }
}
