namespace Dentara.Infrastructure.Waitlist;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Notifications;
using Dentara.Domain.Waitlist;
using Dentara.Infrastructure.Notifications;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Service that processes waitlist entries when a slot opens up.
/// Called by the CancellationService after an appointment is cancelled
/// and by the ExpiryWorker after an appointment expires.
///
/// Flow:
///   1. Find the highest-priority Waiting entry matching (DentistId, ServiceId)
///      whose acceptable range includes the freed slot's start time.
///   2. If found, transition it to Offered with a 30-minute expiry.
///   3. Create a WaitlistOffered notification (SMS) to the patient.
///   4. The patient has 30 minutes to book the appointment.
///      The WaitlistOfferExpiryWorker (runs every 1 min) checks for expired
///      offers and transitions them to Declined, then re-processes the slot.
/// </summary>
public sealed class WaitlistProcessor
{
    private readonly DentaraDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ISmsGateway _smsGateway;
    private readonly ILogger<WaitlistProcessor> _logger;
    private static readonly TimeSpan OfferDuration = TimeSpan.FromMinutes(30);

    public WaitlistProcessor(
        DentaraDbContext dbContext,
        IClock clock,
        ISmsGateway smsGateway,
        ILogger<WaitlistProcessor> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _smsGateway = smsGateway;
        _logger = logger;
    }

    /// <summary>
    /// Called when a slot opens (appointment cancelled or expired).
    /// Attempts to offer the slot to the first matching waitlist entry.
    /// </summary>
    public async Task ProcessFreedSlotAsync(
        Guid dentistId,
        Guid serviceId,
        DateTimeOffset slotStartUtc,
        DateTimeOffset slotEndUtc,
        CancellationToken cancellationToken = default)
    {
        // Find matching waiting entries, ordered by priority (FIFO)
        var matchingEntries = await _dbContext.WaitlistEntries
            .Where(w => w.DentistId == dentistId
                && w.ServiceId == serviceId
                && w.StatusValue == WaitlistStatus.Waiting.Value)
            .ToListAsync(cancellationToken);

        // Filter by acceptable time range (can't do in LINQ — DateTimeOffset)
        var eligible = matchingEntries
            .Where(w => slotStartUtc >= w.EarliestStartUtc && slotStartUtc <= w.LatestStartUtc)
            .OrderBy(w => w.Priority)
            .FirstOrDefault();

        if (eligible == null)
        {
            _logger.LogDebug("WaitlistProcessor: no matching waiting entries for dentist {DentistId} at {SlotStart}",
                dentistId, slotStartUtc);
            return;
        }

        // Offer the slot
        eligible.Offer(slotStartUtc, OfferDuration, _clock);

        // Get patient info for notification
        var patient = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == eligible.PatientId, cancellationToken);
        if (patient == null)
        {
            _logger.LogWarning("WaitlistProcessor: patient {PatientId} not found for waitlist entry {EntryId}",
                eligible.PatientId, eligible.Id);
            return;
        }

        // Get service + dentist names for the notification message
        var service = await _dbContext.Services
            .FirstOrDefaultAsync(s => s.Id == serviceId, cancellationToken);
        var dentist = await _dbContext.DentistProfiles
            .FirstOrDefaultAsync(d => d.Id == dentistId, cancellationToken);

        var persianDateTime = $"{slotStartUtc:yyyy/MM/dd HH:mm} UTC";
        var message = NotificationTemplates.RenderWaitlistOffered(
            dentist?.DisplayName ?? "نامشخص",
            service?.Name ?? "نامشخص",
            persianDateTime);

        // Create notification for immediate delivery
        var notification = Notification.Create(
            eligible.Id, // Use waitlist entry ID as the "appointment" reference
            eligible.PatientId,
            NotificationType.WaitlistOffered,
            NotificationChannel.Sms,
            patient.PhoneNumber,
            message,
            _clock.UtcNow,
            _clock);

        await _dbContext.Notifications.AddAsync(notification, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "WaitlistProcessor: offered slot {SlotStart} to patient {PatientId} (entry {EntryId}), expires at {ExpiresAt}",
            slotStartUtc, eligible.PatientId, eligible.Id, eligible.OfferExpiresAtUtc);
    }
}
