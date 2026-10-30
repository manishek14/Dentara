namespace Dentara.Api.Controllers;

using Dentara.Api.Authorization;
using Dentara.Api.Contracts.Audit;
using Dentara.Api.Contracts.Common;
using Dentara.Api.Extensions;
using Dentara.Application.Audit.Queries;
using Dentara.Application.Common.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Audit log endpoints for viewing the history of sensitive changes.
/// Permission: audit_log.view (Admin only).
///
/// Per the architecture spec:
///   "اصلاح پرونده، تغییر تعرفه، لغو نوبت و تغییر مجوزها باید قابل پیگیری
///    باشد. حذف یا ویرایش اطلاعات حساس نباید ردپای لازم برای حسابرسی را
///    از بین ببرد."
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize]
public sealed class AuditController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IMediator mediator, ILogger<AuditController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Returns a paginated list of audit log entries, optionally filtered.
    ///
    /// Filters:
    ///   - fromUtc / toUtc: date range (defaults to last 30 days)
    ///   - entityType: e.g. "Appointment", "Refund", "WaitlistEntry"
    ///   - entityId: filter to a specific entity
    ///   - action: e.g. "Cancel", "Create", "RequestRefund"
    ///   - performedByUserId: filter to actions by a specific user
    ///   - correlationId: trace a specific request through the system
    ///
    /// Pagination:
    ///   - pageNumber: 1-based (default 1)
    ///   - pageSize: default 50, max 200
    ///
    /// Permission: audit_log.view (Admin only).
    /// </summary>
    [HttpGet("logs")]
    [HasPermission(Permissions.AuditLogView)]
    [ProducesResponseType(typeof(AuditLogPagedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLogs(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] string? action,
        [FromQuery] Guid? performedByUserId,
        [FromQuery] string? correlationId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        // Default to last 30 days if no range specified
        var to = toUtc ?? DateTimeOffset.UtcNow;
        var from = fromUtc ?? to.AddDays(-30);

        var query = new GetAuditLogQuery(
            FromUtc: from,
            ToUtc: to,
            EntityType: entityType,
            EntityId: entityId,
            Action: action,
            PerformedByUserId: performedByUserId,
            CorrelationId: correlationId,
            PageNumber: pageNumber,
            PageSize: pageSize,
            CorrelationIdHeader: null);

        var result = await _mediator.Send(query, cancellationToken);

        return result.ToActionResult(r => new AuditLogPagedResponse(
            r.Items.Select(a => new AuditLogEntryResponse(
                a.Id, a.EntityType, a.EntityId, a.Action,
                a.PerformedBy, a.PerformedByUserId,
                a.OldValueJson, a.NewValueJson,
                a.Description, a.IpAddress, a.CorrelationId,
                a.OccurredAtUtc)).ToList(),
            r.TotalCount,
            r.PageNumber,
            r.PageSize,
            r.TotalPages,
            r.HasNextPage,
            r.HasPreviousPage));
    }

    /// <summary>
    /// Returns the full audit history for a specific entity (e.g. all changes
    /// to a particular appointment).
    ///
    /// Permission: audit_log.view (Admin only).
    /// </summary>
    [HttpGet("entities/{entityType}/{entityId:guid}")]
    [HasPermission(Permissions.AuditLogView)]
    [ProducesResponseType(typeof(AuditLogPagedResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEntityHistory(
        [FromRoute] string entityType,
        [FromRoute] Guid entityId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAuditLogQuery(
            EntityType: entityType,
            EntityId: entityId,
            PageNumber: pageNumber,
            PageSize: pageSize,
            CorrelationIdHeader: null);

        var result = await _mediator.Send(query, cancellationToken);

        return result.ToActionResult(r => new AuditLogPagedResponse(
            r.Items.Select(a => new AuditLogEntryResponse(
                a.Id, a.EntityType, a.EntityId, a.Action,
                a.PerformedBy, a.PerformedByUserId,
                a.OldValueJson, a.NewValueJson,
                a.Description, a.IpAddress, a.CorrelationId,
                a.OccurredAtUtc)).ToList(),
            r.TotalCount,
            r.PageNumber,
            r.PageSize,
            r.TotalPages,
            r.HasNextPage,
            r.HasPreviousPage));
    }
}
