namespace Dentara.Infrastructure.BackgroundJobs;

using Dentara.Domain.Common;
using Dentara.Domain.Waitlist;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Checks for waitlist offers that have expired (patient didn't accept within
/// the offer window). Transitions them to Declined.
///
/// Note: this worker does NOT re-offer the slot automatically. The slot may
/// have already been booked by another patient, or the dentist may have
/// changed their schedule. Re-offering would require checking the slot's
/// current availability, which is done by the caller (CancellationService or
/// ExpiryWorker) when they next call WaitlistProcessor.ProcessFreedSlotAsync.
/// </summary>
public sealed class WaitlistOfferExpiryWorker : WorkerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    public WaitlistOfferExpiryWorker(
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<WaitlistOfferExpiryWorker> logger)
        : base(TimeSpan.FromMinutes(1), logger, errorDelay: TimeSpan.FromMinutes(2))
    {
        _serviceProvider = serviceProvider;
        _clock = clock;
    }

    public override async Task ExecuteIterationAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DentaraDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<WaitlistOfferExpiryWorker>>();

        var now = _clock.UtcNow;

        // Find Offered entries whose offer has expired.
        // Use ADO.NET due to SQLite DateTimeOffset limitations.
        var connection = db.Database.GetDbConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id FROM WaitlistEntries
            WHERE StatusValue = 2
              AND OfferExpiresAtUtc <= $now";
        var pNow = cmd.CreateParameter();
        pNow.ParameterName = "$now";
        pNow.Value = now.ToString("O");
        cmd.Parameters.Add(pNow);

        var expiredIds = new List<Guid>();
        await using (var reader = await cmd.ExecuteReaderAsync(stoppingToken))
        {
            while (await reader.ReadAsync(stoppingToken))
                expiredIds.Add(reader.GetGuid(0));
        }

        if (expiredIds.Count == 0)
            return;

        var expiredEntries = await db.WaitlistEntries
            .Where(w => expiredIds.Contains(w.Id))
            .ToListAsync(stoppingToken);

        logger.LogInformation("WaitlistOfferExpiryWorker: expiring {Count} offers", expiredEntries.Count);

        foreach (var entry in expiredEntries)
        {
            try
            {
                entry.Decline("Offer expired", _clock);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "WaitlistOfferExpiryWorker: failed to expire entry {EntryId}", entry.Id);
            }
        }

        await db.SaveChangesAsync(stoppingToken);
    }
}
