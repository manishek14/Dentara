namespace Dentara.Infrastructure.Audit;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Audit;
using Dentara.Domain.Common;
using Dentara.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

/// <summary>
/// SQL Server-backed implementation of IAuditService. Inserts AuditLog rows
/// that are never updated or deleted (INSERT-only by design).
/// </summary>
public sealed class SqlAuditService : IAuditService
{
    private readonly DentaraDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILogger<SqlAuditService> _logger;

    public SqlAuditService(
        DentaraDbContext dbContext,
        IClock clock,
        ILogger<SqlAuditService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _logger = logger;
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var log = AuditLog.Create(
            entry.EntityType,
            entry.EntityId,
            entry.Action,
            entry.PerformedBy,
            entry.OccurredAtUtc == default ? _clock.UtcNow : entry.OccurredAtUtc,
            entry.Description,
            entry.PerformedByUserId,
            entry.OldValueJson,
            entry.NewValueJson,
            entry.IpAddress,
            entry.CorrelationId);

        await _dbContext.AuditLogs.AddAsync(log, cancellationToken);

        // SaveChanges is called by the caller (UnitOfWork) — we just add to the
        // change tracker. If the caller's transaction rolls back, the audit log
        // entry is also rolled back (which is correct: if the change didn't
        // happen, there's nothing to audit).
    }
}
