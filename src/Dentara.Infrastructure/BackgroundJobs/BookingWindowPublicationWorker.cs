namespace Dentara.Infrastructure.BackgroundJobs;

using Dentara.Domain.Common;
using Dentara.Domain.Scheduling;
using Dentara.Domain.Services.Scheduling;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Background worker that publishes BookingWindows whose OpensAtUtc has passed.
///
/// Scans every 30 seconds for windows in Scheduled status where OpensAtUtc ≤ now.
/// For each, calls IPublicationService.PublishAsync which:
///   - Transitions the window to Open.
///   - Generates PublishedSessions based on each dentist's WorkSchedules
///     (minus ScheduleExceptions).
///   - Idempotent: re-running for the same window produces 0 new sessions
///     (PublishedSessions.IdempotencyKey is UNIQUE per ADR-006).
/// </summary>
public sealed class BookingWindowPublicationWorker : WorkerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    public BookingWindowPublicationWorker(
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<BookingWindowPublicationWorker> logger)
        : base(TimeSpan.FromSeconds(30), logger, errorDelay: TimeSpan.FromMinutes(1))
    {
        _serviceProvider = serviceProvider;
        _clock = clock;
    }

    public override async Task ExecuteIterationAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DentaraDbContext>();
        var publicationService = scope.ServiceProvider.GetRequiredService<IPublicationService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<BookingWindowPublicationWorker>>();

        // Find Scheduled windows whose OpensAtUtc has passed
        var windowsToPublish = await db.BookingWindows
            .Where(w => w.StatusValue == BookingWindowStatus.Scheduled.Value
                && w.OpensAtUtc <= _clock.UtcNow)
            .OrderBy(w => w.OpensAtUtc)
            .Take(10)
            .ToListAsync(stoppingToken);

        if (windowsToPublish.Count == 0)
            return;

        logger.LogInformation(
            "PublicationWorker: publishing {Count} booking windows", windowsToPublish.Count);

        foreach (var window in windowsToPublish)
        {
            try
            {
                var count = await publicationService.PublishAsync(window.Id, stoppingToken);
                logger.LogInformation(
                    "PublicationWorker: published {Count} sessions for window {WindowId}",
                    count, window.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "PublicationWorker: failed to publish window {WindowId}", window.Id);
            }
        }
    }
}
