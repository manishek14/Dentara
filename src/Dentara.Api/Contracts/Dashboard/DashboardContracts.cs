namespace Dentara.Api.Contracts.Dashboard;

public sealed record DashboardStatsResponse(
    int TotalAppointments,
    int PendingPaymentCount,
    int ConfirmedCount,
    int CompletedCount,
    int CancelledCount,
    int ExpiredCount,
    int NoShowCount,
    double NoShowRate,
    decimal TotalRevenue,
    decimal TotalRefunded,
    decimal NetRevenue,
    IReadOnlyList<DentistUtilizationResponse> DentistUtilization,
    IReadOnlyList<PopularServiceResponse> PopularServices,
    IReadOnlyList<AppointmentsByDayResponse> AppointmentsByDay);

public sealed record DentistUtilizationResponse(
    Guid DentistId,
    string DentistName,
    int AppointmentCount,
    int CompletedCount,
    int NoShowCount,
    int CancelledCount,
    decimal BookedHours,
    decimal UtilizationPercent);

public sealed record PopularServiceResponse(
    Guid ServiceId,
    string ServiceName,
    int BookingCount,
    decimal Revenue);

public sealed record AppointmentsByDayResponse(
    DateTimeOffset Date,
    int Count);
