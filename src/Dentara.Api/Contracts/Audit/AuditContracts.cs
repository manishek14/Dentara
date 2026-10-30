namespace Dentara.Api.Contracts.Audit;

public sealed record AuditLogEntryResponse(
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

public sealed record AuditLogPagedResponse(
    IReadOnlyList<AuditLogEntryResponse> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);
