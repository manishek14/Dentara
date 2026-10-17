namespace Dentara.Api.Controllers;

using Dentara.Api.Contracts.Common;
using Dentara.Api.Contracts.Payments;
using Dentara.Api.Extensions;
using Dentara.Api.Middleware;
using Dentara.Application.Payments.Commands;
using Dentara.Domain.Payments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

/// <summary>
/// Payment endpoints: initiate, webhook processing, refund.
/// </summary>
[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IMediator mediator, ILogger<PaymentsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Initiates a payment for an appointment. Returns the gateway redirect URL.
    /// The patient navigates to the URL, completes payment at the gateway, and
    /// the gateway calls back to the webhook endpoint.
    /// </summary>
    [HttpPost("initiate")]
    [Authorize]
    [ProducesResponseType(typeof(InitiatePaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Initiate(
        [FromBody] InitiatePaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AppointmentId == Guid.Empty || request.Amount <= 0)
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                "AppointmentId and positive Amount are required."));

        var userId = GetCurrentUserId();

        var command = new InitiatePaymentCommand(
            request.AppointmentId,
            userId,
            request.Amount,
            request.Currency,
            request.CallbackUrl,
            HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);

        return result.ToActionResult(r => new InitiatePaymentResponse(
            r.PaymentAttemptId, r.RedirectUrl));
    }

    /// <summary>
    /// Receives a webhook callback from a payment gateway. This endpoint is
    /// called BY THE GATEWAY, not by the patient. Idempotent: re-processing
    /// the same ProviderEventId returns the original result without
    /// double-charging or double-confirming.
    /// </summary>
    [HttpPost("webhooks")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProcessWebhookResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ProcessWebhook(
        [FromBody] ProcessWebhookRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Provider) ||
            string.IsNullOrWhiteSpace(request.ProviderEventId))
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                "Provider and ProviderEventId are required."));

        PaymentProvider provider;
        try
        {
            provider = PaymentProvider.FromName(request.Provider);
        }
        catch (ArgumentException)
        {
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                $"Unknown payment provider: {request.Provider}"));
        }

        var command = new ProcessWebhookCommand(
            provider,
            request.ProviderEventId,
            request.EventType,
            request.RawPayload,
            request.SignatureHeader,
            HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);

        return result.ToActionResult(r => new ProcessWebhookResponse(
            r.WasDuplicate, r.PaymentAttemptId, r.ProcessingNotes));
    }

    /// <summary>
    /// Requests a refund for a confirmed-then-cancelled appointment. The
    /// refund amount is computed from the appointment's snapshot fields.
    /// </summary>
    [HttpPost("refunds")]
    [Authorize(Roles = "Admin,Receptionist")]
    [ProducesResponseType(typeof(RequestRefundResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestRefund(
        [FromBody] RequestRefundRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PaymentAttemptId == Guid.Empty || request.RefundAmount <= 0)
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                "PaymentAttemptId and positive RefundAmount are required."));

        var command = new RequestRefundCommand(
            request.PaymentAttemptId,
            request.AppointmentId,
            request.RefundAmount,
            request.Currency,
            request.Reason,
            HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);

        return result.ToActionResult(r => new RequestRefundResponse(
            r.RefundId, r.Status));
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User is not authenticated.");
        return Guid.Parse(sub);
    }
}
