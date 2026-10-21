namespace Dentara.Infrastructure.BackgroundJobs;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Identity;
using Dentara.Domain.Notifications;
using Dentara.Infrastructure.Notifications;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Scans Confirmed appointments and schedules reminder notifications for
/// appointments that are ~24h away and don't have a reminder yet.
///
/// The reminder is sent 24 hours before the appointment start time. If the
/// appointment is booked less than 24h before its start, no reminder is
/// scheduled (the patient just booked it — they know).
/// </summary>
public sealed class ReminderSchedulingWorker : WorkerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    public ReminderSchedulingWorker(
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<ReminderSchedulingWorker> logger)
        : base(TimeSpan.FromMinutes(5), logger, errorDelay: TimeSpan.FromMinutes(10))
    {
        _serviceProvider = serviceProvider;
        _clock = clock;
    }

    public override async Task ExecuteIterationAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DentaraDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ReminderSchedulingWorker>>();

        var now = _clock.UtcNow;
        var reminderWindow = now.AddHours(24);

        // Find Confirmed appointments starting within the next 24 hours.
        // Use ADO.NET for the initial query because SQLite can't translate
        // DateTimeOffset comparisons in LINQ.
        var connection = db.Database.GetDbConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT a.Id, a.PatientId, a.DentistId, a.ServiceNameSnapshot, a.DentistDisplayNameSnapshot, a.StartAtUtc
            FROM Appointments a
            WHERE a.StatusValue = 2
              AND a.StartAtUtc > $now
              AND a.StartAtUtc <= $reminderWindow
              AND NOT EXISTS (
                SELECT 1 FROM Notifications n
                WHERE n.AppointmentId = a.Id
                  AND n.TypeValue = 1
                  AND n.StatusValue IN (1, 2)
              )";
        var pNow = cmd.CreateParameter();
        pNow.ParameterName = "$now";
        pNow.Value = now.ToString("O");
        cmd.Parameters.Add(pNow);
        var pWindow = cmd.CreateParameter();
        pWindow.ParameterName = "$reminderWindow";
        pWindow.Value = reminderWindow.ToString("O");
        cmd.Parameters.Add(pWindow);

        var candidates = new List<(Guid Id, Guid PatientId, Guid DentistId, string ServiceName, string DentistName, DateTimeOffset StartUtc)>();
        await using (var reader = await cmd.ExecuteReaderAsync(stoppingToken))
        {
            while (await reader.ReadAsync(stoppingToken))
            {
                candidates.Add((
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    reader.GetGuid(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    new DateTimeOffset(reader.GetDateTime(5), TimeSpan.Zero)));
            }
        }

        if (candidates.Count == 0)
            return;

        logger.LogInformation("ReminderSchedulingWorker: scheduling {Count} reminders", candidates.Count);

        // Load patient phone numbers
        var patientIds = candidates.Select(c => c.PatientId).Distinct().ToList();
        var patients = await db.Users
            .Where(u => patientIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, stoppingToken);

        var newNotifications = new List<Notification>();
        foreach (var c in candidates)
        {
            if (!patients.TryGetValue(c.PatientId, out var patient))
            {
                logger.LogWarning("ReminderSchedulingWorker: patient {PatientId} not found for appointment {AppointmentId}",
                    c.PatientId, c.Id);
                continue;
            }

            // Schedule the reminder 24h before the appointment start
            var reminderTime = c.StartUtc.AddHours(-24);
            if (reminderTime < now)
                reminderTime = now; // appointment is <24h away — send immediately

            var persianDateTime = $"{c.StartUtc:yyyy/MM/dd HH:mm} UTC";
            var message = NotificationTemplates.RenderAppointmentReminder(
                c.DentistName, c.ServiceName, c.StartUtc, persianDateTime);

            var notification = Notification.Create(
                c.Id,
                c.PatientId,
                NotificationType.AppointmentReminder,
                NotificationChannel.Sms,
                patient.PhoneNumber,
                message,
                reminderTime,
                _clock);

            newNotifications.Add(notification);
        }

        await db.Notifications.AddRangeAsync(newNotifications, stoppingToken);
        await db.SaveChangesAsync(stoppingToken);

        logger.LogInformation("ReminderSchedulingWorker: created {Count} reminder notifications", newNotifications.Count);
    }
}
