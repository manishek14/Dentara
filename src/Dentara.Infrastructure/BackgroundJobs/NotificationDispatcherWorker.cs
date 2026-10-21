namespace Dentara.Infrastructure.BackgroundJobs;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Common;
using Dentara.Domain.Notifications;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Dispatches pending notifications that are due. Uses ISmsGateway to send
/// SMS notifications. Handles retries (up to 3 attempts) for failed sends.
///
/// Per the architecture spec: the sandbox SMS gateway logs to console.
/// Production swaps in the real sms.ir adapter via DI configuration.
/// </summary>
public sealed class NotificationDispatcherWorker : WorkerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private const int MaxAttempts = 3;

    public NotificationDispatcherWorker(
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<NotificationDispatcherWorker> logger)
        : base(TimeSpan.FromSeconds(30), logger, errorDelay: TimeSpan.FromMinutes(1))
    {
        _serviceProvider = serviceProvider;
        _clock = clock;
    }

    public override async Task ExecuteIterationAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DentaraDbContext>();
        var smsGateway = scope.ServiceProvider.GetRequiredService<ISmsGateway>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<NotificationDispatcherWorker>>();

        var now = _clock.UtcNow;

        // Find due pending notifications (and failed ones eligible for retry).
        // Use ADO.NET for the initial query due to SQLite DateTimeOffset limitations.
        var connection = db.Database.GetDbConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id FROM Notifications
            WHERE (StatusValue = 1 AND ScheduledAtUtc <= $now)
               OR (StatusValue = 3 AND AttemptCount < $maxAttempts)
            ORDER BY ScheduledAtUtc
            LIMIT 50";
        var pNow = cmd.CreateParameter();
        pNow.ParameterName = "$now";
        pNow.Value = now.ToString("O");
        cmd.Parameters.Add(pNow);
        var pMax = cmd.CreateParameter();
        pMax.ParameterName = "$maxAttempts";
        pMax.Value = MaxAttempts;
        cmd.Parameters.Add(pMax);

        var notificationIds = new List<Guid>();
        await using (var reader = await cmd.ExecuteReaderAsync(stoppingToken))
        {
            while (await reader.ReadAsync(stoppingToken))
                notificationIds.Add(reader.GetGuid(0));
        }

        if (notificationIds.Count == 0)
            return;

        // Load the notification entities (tracked)
        var notifications = await db.Notifications
            .Where(n => notificationIds.Contains(n.Id))
            .ToListAsync(stoppingToken);

        logger.LogInformation("NotificationDispatcher: sending {Count} notifications", notifications.Count);

        foreach (var notification in notifications)
        {
            try
            {
                if (notification.Channel == NotificationChannel.Sms)
                {
                    var success = await smsGateway.SendAsync(
                        notification.PhoneNumber,
                        notification.MessageBody,
                        stoppingToken);

                    if (success)
                    {
                        notification.MarkSent(_clock);
                        logger.LogInformation("NotificationDispatcher: sent notification {NotificationId} to {Phone}",
                            notification.Id, notification.PhoneNumber);
                    }
                    else
                    {
                        notification.MarkFailed("SMS gateway returned false", _clock);
                        logger.LogWarning("NotificationDispatcher: SMS gateway failed for notification {NotificationId}",
                            notification.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                notification.MarkFailed(ex.Message, _clock);
                logger.LogError(ex, "NotificationDispatcher: exception sending notification {NotificationId}",
                    notification.Id);
            }
        }

        await db.SaveChangesAsync(stoppingToken);
    }
}
