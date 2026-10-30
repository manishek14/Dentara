namespace Dentara.Api.Controllers;

using Dentara.Api.Authorization;
using Dentara.Api.Contracts.Common;
using Dentara.Api.Contracts.Waitlist;
using Dentara.Api.Extensions;
using Dentara.Api.Middleware;
using Dentara.Application.Common.Authorization;
using Dentara.Application.Waitlist.Commands;
using Dentara.Application.Waitlist.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

/// <summary>
/// Waitlist endpoints: join, cancel, list, get by ID.
///
/// Permission mapping:
///   waitlist.join       → Patient, Admin (join the waitlist)
///   waitlist.view_all   → Receptionist, Admin (view all entries)
///   waitlist.manage     → Receptionist, Admin (cancel any entry)
///
/// Patients can view and cancel their own entries without the view_all/manage
/// permissions — the handler checks ownership.
/// </summary>
[ApiController]
[Route("api/waitlist")]
[Authorize]
public sealed class WaitlistController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<WaitlistController> _logger;

    public WaitlistController(IMediator mediator, ILogger<WaitlistController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Adds the current patient to the waitlist for a specific dentist + service
    /// + time range. When a matching slot opens, they'll receive an SMS offer.
    /// Idempotent: send X-Idempotency-Key header on retries.
    /// Permission: waitlist.join (Patient, Admin).
    /// </summary>
    [HttpPost]
    [HasPermission(Permissions.WaitlistJoin)]
    [ProducesResponseType(typeof(JoinWaitlistResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Join(
        [FromBody] JoinWaitlistRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DentistId == Guid.Empty || request.ServiceId == Guid.Empty)
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                "DentistId and ServiceId are required."));

        if (request.EarliestStartUtc >= request.LatestStartUtc)
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                "EarliestStartUtc must be before LatestStartUtc."));

        var patientId = GetCurrentUserId();

        var command = new JoinWaitlistCommand(
            patientId,
            request.DentistId,
            request.ServiceId,
            request.EarliestStartUtc,
            request.LatestStartUtc,
            request.Notes,
            HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);

        return result.ToActionResult(r => new JoinWaitlistResponse(
            r.WaitlistEntryId, "Waiting", DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Cancels a waitlist entry. Patients can cancel their own; staff can
    /// cancel any.
    /// Permission: waitlist.join (patient cancels own) OR
    ///             waitlist.manage (staff cancels any).
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [HasPermission(Permissions.WaitlistJoin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(
        [FromRoute] Guid id,
        [FromBody] CancelWaitlistRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        // Staff can cancel on behalf of a patient
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var isStaff = roles.Contains(nameof(Dentara.Domain.Identity.Role.Admin)) ||
                       roles.Contains(nameof(Dentara.Domain.Identity.Role.Receptionist));

        var patientId = request?.PatientId ?? userId;
        var cancelledBy = isStaff ? $"Staff:{userId}" : $"Patient:{userId}";

        var command = new CancelWaitlistCommand(
            id,
            patientId,
            HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Returns a paginated list of waitlist entries.
    /// Patients see only their own; staff with waitlist.view_all see all.
    /// Query params: patientId, dentistId, status, pageNumber, pageSize.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? patientId,
        [FromQuery] Guid? dentistId,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var canViewAll = roles.Any(r =>
            RolePermissions.GetForRole(r).Contains(Permissions.WaitlistViewAll));

        // If the user doesn't have view_all, force the filter to their own entries
        var effectivePatientId = canViewAll ? patientId : userId;

        var query = new GetWaitlistEntriesQuery(
            PatientId: effectivePatientId,
            DentistId: dentistId,
            Status: status,
            PageNumber: pageNumber,
            PageSize: pageSize,
            CorrelationIdHeader: HttpContext.GetCorrelationId());

        var result = await _mediator.Send(query, cancellationToken);

        return result.ToActionResult(r => new
        {
            items = r.Items.Select(w => new WaitlistEntryResponse(
                w.Id, w.PatientId, w.DentistId, w.ServiceId,
                w.EarliestStartUtc, w.LatestStartUtc, w.Status, w.Priority,
                w.OfferedAtUtc, w.OfferExpiresAtUtc, w.OfferedStartUtc,
                w.FulfilledAtUtc, w.DeclinedAtUtc, w.CancelledAtUtc,
                w.DeclineReason, w.Notes, w.CreatedAtUtc)).ToList(),
            totalCount = r.TotalCount,
            pageNumber = r.PageNumber,
            pageSize = r.PageSize,
            totalPages = r.TotalPages,
            hasNextPage = r.HasNextPage,
            hasPreviousPage = r.HasPreviousPage
        });
    }

    /// <summary>
    /// Returns a single waitlist entry by ID.
    /// Patients can only view their own; staff can view any.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WaitlistEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetWaitlistEntryByIdQuery(
            id,
            HttpContext.GetCorrelationId());

        var result = await _mediator.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return result.ToActionResult();

        var entry = result.Value!;

        // Ownership check: patients can only view their own entries
        var userId = GetCurrentUserId();
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
        var canViewAll = roles.Any(r =>
            RolePermissions.GetForRole(r).Contains(Permissions.WaitlistViewAll));

        if (!canViewAll && entry.PatientId != userId)
            return Forbid();

        return Ok(new WaitlistEntryResponse(
            entry.Id, entry.PatientId, entry.DentistId, entry.ServiceId,
            entry.EarliestStartUtc, entry.LatestStartUtc, entry.Status, entry.Priority,
            entry.OfferedAtUtc, entry.OfferExpiresAtUtc, entry.OfferedStartUtc,
            entry.FulfilledAtUtc, entry.DeclinedAtUtc, entry.CancelledAtUtc,
            entry.DeclineReason, entry.Notes, entry.CreatedAtUtc));
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User is not authenticated.");
        return Guid.Parse(sub);
    }
}
