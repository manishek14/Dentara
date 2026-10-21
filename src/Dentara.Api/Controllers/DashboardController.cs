namespace Dentara.Api.Controllers;

using Dentara.Api.Authorization;
using Dentara.Api.Contracts.Common;
using Dentara.Api.Contracts.Dashboard;
using Dentara.Api.Extensions;
using Dentara.Api.Middleware;
using Dentara.Application.Common.Authorization;
using Dentara.Application.Dashboard.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Dashboard endpoints for clinic management. Admin and Receptionist only.
/// </summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IMediator mediator, ILogger<DashboardController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Returns aggregate statistics for the dashboard:
    /// - Total appointments by status
    /// - No-show rate
    /// - Revenue (total collected, total refunded, net)
    /// - Dentist utilization (booked hours, completion rate)
    /// - Top 5 popular services by booking count
    /// - Appointments by day (for chart)
    ///
    /// Requires the DashboardView permission (Admin, Receptionist, Dentist).
    /// </summary>
    [HttpGet("stats")]
    [HasPermission(Permissions.DashboardView)]
    [ProducesResponseType(typeof(DashboardStatsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStats(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        CancellationToken cancellationToken)
    {
        // Default to last 30 days if no range specified
        var to = toUtc ?? DateTimeOffset.UtcNow;
        var from = fromUtc ?? to.AddDays(-30);

        if (from >= to)
            return BadRequest(new ApiErrorResponse("VALIDATION_ERROR",
                "FromUtc must be before ToUtc."));

        var query = new GetDashboardStatsQuery(from, to, HttpContext.GetCorrelationId());
        var result = await _mediator.Send(query, cancellationToken);

        return result.ToActionResult(r => new DashboardStatsResponse(
            r.TotalAppointments,
            r.PendingPaymentCount,
            r.ConfirmedCount,
            r.CompletedCount,
            r.CancelledCount,
            r.ExpiredCount,
            r.NoShowCount,
            r.NoShowRate,
            r.TotalRevenue,
            r.TotalRefunded,
            r.NetRevenue,
            r.DentistUtilization.Select(d => new DentistUtilizationResponse(
                d.DentistId, d.DentistName, d.AppointmentCount,
                d.CompletedCount, d.NoShowCount, d.CancelledCount,
                d.BookedHours, d.UtilizationPercent)).ToList(),
            r.PopularServices.Select(s => new PopularServiceResponse(
                s.ServiceId, s.ServiceName, s.BookingCount, s.Revenue)).ToList(),
            r.AppointmentsByDay.Select(d => new AppointmentsByDayResponse(
                d.Date, d.Count)).ToList()));
    }
}
