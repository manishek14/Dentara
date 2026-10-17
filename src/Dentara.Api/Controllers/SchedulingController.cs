namespace Dentara.Api.Controllers;

using Dentara.Api.Contracts.Common;
using Dentara.Api.Contracts.Scheduling;
using Dentara.Api.Extensions;
using Dentara.Api.Middleware;
using Dentara.Application.Scheduling.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Scheduling endpoints: booking window publication. Admin only.
/// </summary>
[ApiController]
[Route("api/scheduling")]
[Authorize(Roles = "Admin")]
public sealed class SchedulingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SchedulingController> _logger;

    public SchedulingController(IMediator mediator, ILogger<SchedulingController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Triggers publication of sessions for a BookingWindow. Normally called
    /// by the BackgroundWorker, but exposed as an endpoint so admins can
    /// manually re-trigger if needed. Idempotent.
    /// </summary>
    [HttpPost("booking-windows/{id:guid}/publish")]
    [ProducesResponseType(typeof(PublishBookingWindowResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PublishBookingWindow(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var command = new PublishBookingWindowCommand(
            id,
            HttpContext.GetCorrelationId());

        var result = await _mediator.Send(command, cancellationToken);

        return result.ToActionResult(r => new PublishBookingWindowResponse(
            r.SessionsPublished, r.WasReplayed));
    }
}
