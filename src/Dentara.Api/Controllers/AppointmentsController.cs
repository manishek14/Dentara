namespace Dentara.Api.Controllers;

using Dentara.Api.Contracts.Booking;
using Dentara.Api.Contracts.Common;
using Dentara.Api.Extensions;
using Dentara.Api.Middleware;
using Dentara.Application.Booking.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

/// <summary>
/// Appointment endpoints: create, cancel, confirm, expire.
/// All endpoints require JWT authentication.
/// </summary>
[ApiController]
[Route("api/appointments")]
[Authorize]
public sealed class AppointmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AppointmentsController> _logger;

    public AppointmentsController(IMediator mediator, ILogger<AppointmentsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new appointment. The booking transaction runs with UPDLOCK+
    /// HOLDLOCK per ADR-003 — if the slot is taken, returns 409 Conflict.
    /// Idempotent: send X-Idempotency-Key header on retries.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateAppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAppointmentRequest request,
        [FromHeader(Name = ApiHeaders.IdempotencyKey)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (request.DentistId == Guid.Empty || request.ServiceId == Guid.Empty)
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                "DentistId and ServiceId are required."));

        var patientId = GetCurrentUserId();

        var command = new CreateAppointmentCommand(
            patientId,
            request.DentistId,
            request.ServiceId,
            request.PublishedSessionId,
            request.StartAtUtc,
            request.EndAtUtc,
            request.ServiceNameSnapshot,
            request.ServiceDurationMinutesSnapshot,
            request.PriceAmountSnapshot,
            request.CurrencySnapshot,
            request.DentistDisplayNameSnapshot,
            request.CancellationPolicyVersion,
            request.CancellationFeePercent,
            request.RefundablePercent,
            request.TermsSnapshotHash,
            request.RequiredResources
                .Select(r => new Application.Booking.Commands.ResourceRequirementItem(
                    r.ResourceTypeValue, r.QuantityRequired, r.RequireDentistOwned, r.SpecificResourceId))
                .ToList(),
            HttpContext.GetCorrelationId() ?? idempotencyKey,
            request.PaymentWindowOverride);

        var result = await _mediator.Send(command, cancellationToken);

        return result.ToActionResult(r => new CreateAppointmentResponse(
            r.AppointmentId, r.ExpiresAtUtc, r.PriceAmount, r.Currency));
    }

    /// <summary>
    /// Cancels an appointment. Releases capacity atomically and creates a
    /// Refund row if the appointment was Confirmed. Idempotent.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(
        [FromRoute] Guid id,
        [FromBody] CancelAppointmentRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var cancelledBy = $"Patient:{userId}";

        var command = new CancelAppointmentCommand(
            id,
            cancelledBy,
            request?.Reason,
            HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);
        return result.ToActionResult();
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User is not authenticated.");
        return Guid.Parse(sub);
    }
}
