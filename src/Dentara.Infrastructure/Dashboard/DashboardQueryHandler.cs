namespace Dentara.Infrastructure.Dashboard;

using Dentara.Application.Common.Results;
using Dentara.Application.Dashboard.Queries;
using Dentara.Domain.Booking;
using Dentara.Domain.Payments;
using Dentara.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed class GetDashboardStatsQueryHandler
    : IRequestHandler<GetDashboardStatsQuery, Result<DashboardStats>>
{
    private readonly DentaraDbContext _dbContext;

    public GetDashboardStatsQueryHandler(DentaraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<DashboardStats>> Handle(GetDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var fromUtc = request.FromUtc;
            var toUtc = request.ToUtc;

            // Appointment counts by status — load all in range, aggregate on client
            // (SQLite can't translate DateTimeOffset comparisons in GroupBy)
            var appointments = await _dbContext.Appointments
                .Where(a => a.StartAtUtc >= fromUtc && a.StartAtUtc <= toUtc)
                .ToListAsync(cancellationToken);

            var totalAppointments = appointments.Count;
            var pendingCount = appointments.Count(a => a.StatusValue == AppointmentStatus.PendingPayment.Value);
            var confirmedCount = appointments.Count(a => a.StatusValue == AppointmentStatus.Confirmed.Value);
            var completedCount = appointments.Count(a => a.StatusValue == AppointmentStatus.Completed.Value);
            var cancelledCount = appointments.Count(a => a.StatusValue == AppointmentStatus.Cancelled.Value);
            var expiredCount = appointments.Count(a => a.StatusValue == AppointmentStatus.Expired.Value);
            var noShowCount = appointments.Count(a => a.StatusValue == AppointmentStatus.NoShow.Value);

            var noShowRate = (completedCount + noShowCount) > 0
                ? (double)noShowCount / (completedCount + noShowCount) * 100.0
                : 0.0;

            // Revenue — sum of succeeded payments in range
            var payments = await _dbContext.PaymentAttempts
                .Where(p => p.SucceededAtUtc >= fromUtc && p.SucceededAtUtc <= toUtc
                    && p.StatusValue == PaymentStatus.Succeeded.Value)
                .ToListAsync(cancellationToken);
            var totalRevenue = payments.Sum(p => p.Amount);

            // Refunds — sum of succeeded refunds in range
            var refunds = await _dbContext.Refunds
                .Where(r => r.SucceededAtUtc >= fromUtc && r.SucceededAtUtc <= toUtc
                    && r.StatusValue == RefundStatus.Succeeded.Value)
                .ToListAsync(cancellationToken);
            var totalRefunded = refunds.Sum(r => r.Amount);
            var netRevenue = totalRevenue - totalRefunded;

            // Dentist utilization
            var dentistIds = appointments.Select(a => a.DentistId).Distinct().ToList();
            var dentists = await _dbContext.DentistProfiles
                .Where(d => dentistIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, cancellationToken);

            var dentistUtilization = appointments
                .GroupBy(a => a.DentistId)
                .Select(g =>
                {
                    var bookedHours = g.Sum(a => (a.EndAtUtc - a.StartAtUtc).TotalHours);
                    dentists.TryGetValue(g.Key, out var dentist);
                    return new DentistUtilization(
                        g.Key,
                        dentist?.DisplayName ?? "Unknown",
                        g.Count(),
                        g.Count(a => a.StatusValue == AppointmentStatus.Completed.Value),
                        g.Count(a => a.StatusValue == AppointmentStatus.NoShow.Value),
                        g.Count(a => a.StatusValue == AppointmentStatus.Cancelled.Value),
                        (decimal)bookedHours,
                        0m); // AvailableHours requires schedule data — left as 0 for now
                })
                .ToList();

            // Popular services (top 5 by booking count)
            var popularServices = appointments
                .GroupBy(a => new { a.ServiceId, a.ServiceNameSnapshot })
                .Select(g => new PopularService(
                    g.Key.ServiceId,
                    g.Key.ServiceNameSnapshot,
                    g.Count(),
                    g.Where(a => a.StatusValue == AppointmentStatus.Completed.Value)
                     .Sum(a => a.PriceAmountSnapshot)))
                .OrderByDescending(s => s.BookingCount)
                .Take(5)
                .ToList();

            // Appointments by day
            var byDay = appointments
                .GroupBy(a => a.StartAtUtc.Date)
                .Select(g => new AppointmentsByDay(
                    new DateTimeOffset(g.Key, TimeSpan.Zero),
                    g.Count()))
                .OrderBy(d => d.Date)
                .ToList();

            var stats = new DashboardStats(
                totalAppointments,
                pendingCount,
                confirmedCount,
                completedCount,
                cancelledCount,
                expiredCount,
                noShowCount,
                Math.Round(noShowRate, 2),
                totalRevenue,
                totalRefunded,
                netRevenue,
                dentistUtilization,
                popularServices,
                byDay);

            return Result<DashboardStats>.Success(stats);
        }
        catch (Exception ex)
        {
            return Result<DashboardStats>.FromException(ex);
        }
    }
}
