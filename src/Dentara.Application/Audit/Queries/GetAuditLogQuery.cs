namespace Dentara.Application.Audit.Queries;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Results;
using MediatR;

/// <summary>
/// Returns a paginated list of audit log entries, optionally filtered by
/// entity type, entity ID, action, performed-by user, or date range.
///
/// Results are ordered by OccurredAtUtc descending (most recent first).
/// Requires the audit_log.view permission (Admin only).
/// </summary>
public sealed record GetAuditLogQuery(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    string? EntityType = null,
    Guid? EntityId = null,
    string? Action = null,
    Guid? PerformedByUserId = null,
    string? CorrelationId = null,
    int PageNumber = 1,
    int PageSize = 50,
    string? CorrelationIdHeader = null)
    : IQuery<PagedResult<AuditLogEntry>>, IHasCorrelationId
{
    string? IHasCorrelationId.CorrelationId => CorrelationIdHeader;
}

/// <summary>
/// A single audit log entry in the response.
/// </summary>
public sealed record AuditLogEntry(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Action,
    string PerformedBy,
    Guid? PerformedByUserId,
    string? OldValueJson,
    string? NewValueJson,
    string Description,
    string? IpAddress,
    string? CorrelationId,
    DateTimeOffset OccurredAtUtc);
