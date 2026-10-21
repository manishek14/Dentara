namespace Dentara.Application.Dashboard.Queries;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Results;
using MediatR;

/// <summary>
/// Returns aggregate statistics for the management dashboard:
/// - Total appointments (by status)
/// - No-show rate
/// - Dentist utilization (booked vs available hours)
/// - Revenue (total collected, total refunded)
/// - Most popular services (top 5 by booking count)
///
/// All counts are for the specified date range (UTC).
/// </summary>
public sealed record GetDashboardStatsQuery(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string? CorrelationId = null)
    : IQuery<DashboardStats>, IHasCorrelationId
{
}

/// <summary>
/// Aggregate statistics for the dashboard.
/// </summary>
public sealed record DashboardStats(
    int TotalAppointments,
    int PendingPaymentCount,
    int ConfirmedCount,
    int CompletedCount,
    int CancelledCount,
    int ExpiredCount,
    int NoShowCount,
    double NoShowRate,             // NoShowCount / (CompletedCount + NoShowCount) * 100
    decimal TotalRevenue,          // sum of succeeded payments
    decimal TotalRefunded,         // sum of succeeded refunds
    decimal NetRevenue,            // TotalRevenue - TotalRefunded
    IReadOnlyList<DentistUtilization> DentistUtilization,
    IReadOnlyList<PopularService> PopularServices,
    IReadOnlyList<AppointmentsByDay> AppointmentsByDay);

public sealed record DentistUtilization(
    Guid DentistId,
    string DentistName,
    int AppointmentCount,
    int CompletedCount,
    int NoShowCount,
    int CancelledCount,
    decimal BookedHours,
    decimal UtilizationPercent);   // BookedHours / AvailableHours * 100 (if available)

public sealed record PopularService(
    Guid ServiceId,
    string ServiceName,
    int BookingCount,
    decimal Revenue);

public sealed record AppointmentsByDay(
    DateTimeOffset Date,
    int Count);
