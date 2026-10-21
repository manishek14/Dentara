namespace Dentara.Domain.Waitlist;

using Dentara.Domain.Common;

/// <summary>
/// A patient's request to be notified when a specific dentist's slot becomes
/// available for a specific service within a date range.
///
/// When a slot opens (via appointment cancellation or expiry), the
/// WaitlistProcessorWorker finds the highest-priority Waiting entry that
/// matches the slot's (DentistId, ServiceId) and transitions it to Offered.
/// The patient has a limited time (OfferExpiresAtUtc, default 30 min) to
/// accept; if they don't, the entry is Declined and the next patient is offered.
/// </summary>
public sealed class WaitlistEntry : AggregateRoot
{
    public Guid PatientId { get; private set; }
    public Guid DentistId { get; private set; }
    public Guid ServiceId { get; private set; }

    /// <summary>
    /// The earliest UTC time the patient is willing to be seen.
    /// </summary>
    public DateTimeOffset EarliestStartUtc { get; private set; }

    /// <summary>
    /// The latest UTC time the patient is willing to be seen.
    /// </summary>
    public DateTimeOffset LatestStartUtc { get; private set; }

    public int StatusValue { get; private set; }
    public WaitlistStatus Status => WaitlistStatus.FromValue(StatusValue);

    /// <summary>
    /// Priority — lower number = higher priority. Defaults to the creation
    /// timestamp ticks so earlier entries get priority (FIFO within same
    /// dentist+service).
    /// </summary>
    public long Priority { get; private set; }

    /// <summary>
    /// When an offer was made (if Status = Offered).
    /// </summary>
    public DateTimeOffset? OfferedAtUtc { get; private set; }

    /// <summary>
    /// When the current offer expires (if Status = Offered).
    /// </summary>
    public DateTimeOffset? OfferExpiresAtUtc { get; private set; }

    /// <summary>
    /// The appointment slot that was offered (if Status = Offered/Fulfilled).
    /// </summary>
    public DateTimeOffset? OfferedStartUtc { get; private set; }

    public DateTimeOffset? FulfilledAtUtc { get; private set; }
    public DateTimeOffset? DeclinedAtUtc { get; private set; }
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    public string? DeclineReason { get; private set; }

    /// <summary>
    /// Notes from the patient (e.g. "prefer mornings").
    /// </summary>
    public string? Notes { get; private set; }

    private WaitlistEntry() { } // EF Core

    public static WaitlistEntry Create(
        Guid patientId,
        Guid dentistId,
        Guid serviceId,
        DateTimeOffset earliestStartUtc,
        DateTimeOffset latestStartUtc,
        IClock clock,
        string? notes = null)
    {
        if (patientId == Guid.Empty) throw new ArgumentException("PatientId required.");
        if (dentistId == Guid.Empty) throw new ArgumentException("DentistId required.");
        if (serviceId == Guid.Empty) throw new ArgumentException("ServiceId required.");
        if (earliestStartUtc.Offset != TimeSpan.Zero) throw new ArgumentException("EarliestStartUtc must be UTC.");
        if (latestStartUtc.Offset != TimeSpan.Zero) throw new ArgumentException("LatestStartUtc must be UTC.");
        if (earliestStartUtc >= latestStartUtc)
            throw new ArgumentException("EarliestStartUtc must be before LatestStartUtc.");

        return new WaitlistEntry
        {
            PatientId = patientId,
            DentistId = dentistId,
            ServiceId = serviceId,
            EarliestStartUtc = earliestStartUtc,
            LatestStartUtc = latestStartUtc,
            StatusValue = WaitlistStatus.Waiting.Value,
            // Priority = creation timestamp ticks → FIFO ordering
            Priority = clock.UtcNow.Ticks,
            Notes = notes,
            CreatedAtUtc = clock.UtcNow
        };
    }

    /// <summary>
    /// Transitions to Offered. Sets OfferExpiresAtUtc to now + offerDuration.
    /// Only legal from Waiting.
    /// </summary>
    public void Offer(DateTimeOffset slotStartUtc, TimeSpan offerDuration, IClock clock)
    {
        if (Status != WaitlistStatus.Waiting)
            throw new InvalidOperationException(
                $"Cannot offer a waitlist entry in status {Status}.");

        if (slotStartUtc < EarliestStartUtc || slotStartUtc > LatestStartUtc)
            throw new InvalidOperationException(
                $"Offered slot {slotStartUtc} is outside the patient's acceptable range " +
                $"[{EarliestStartUtc}, {LatestStartUtc}].");

        StatusValue = WaitlistStatus.Offered.Value;
        OfferedAtUtc ??= clock.UtcNow;
        OfferExpiresAtUtc = clock.UtcNow + offerDuration;
        OfferedStartUtc = slotStartUtc;
        Touch(clock.UtcNow);
    }

    /// <summary>
    /// Patient accepted the offer and booked the appointment.
    /// </summary>
    public void Fulfill(IClock clock)
    {
        if (Status != WaitlistStatus.Offered)
            throw new InvalidOperationException(
                $"Cannot fulfill a waitlist entry in status {Status}.");

        if (OfferExpiresAtUtc.HasValue && clock.UtcNow > OfferExpiresAtUtc.Value)
            throw new InvalidOperationException("Offer has expired.");

        StatusValue = WaitlistStatus.Fulfilled.Value;
        FulfilledAtUtc ??= clock.UtcNow;
        Touch(clock.UtcNow);
    }

    /// <summary>
    /// Patient declined the offer, or the offer expired.
    /// </summary>
    public void Decline(string? reason, IClock clock)
    {
        if (Status != WaitlistStatus.Offered)
            throw new InvalidOperationException(
                $"Cannot decline a waitlist entry in status {Status}.");

        StatusValue = WaitlistStatus.Declined.Value;
        DeclinedAtUtc ??= clock.UtcNow;
        DeclineReason = reason;
        Touch(clock.UtcNow);
    }

    /// <summary>
    /// Patient cancels their waitlist entry (before any offer).
    /// </summary>
    public void Cancel(IClock clock)
    {
        if (Status != WaitlistStatus.Waiting)
            throw new InvalidOperationException(
                $"Cannot cancel a waitlist entry in status {Status}.");

        StatusValue = WaitlistStatus.Cancelled.Value;
        CancelledAtUtc ??= clock.UtcNow;
        Touch(clock.UtcNow);
    }

    /// <summary>
    /// Returns true if the current offer has expired.
    /// </summary>
    public bool IsOfferExpired(IClock clock) =>
        Status == WaitlistStatus.Offered &&
        OfferExpiresAtUtc.HasValue &&
        clock.UtcNow > OfferExpiresAtUtc.Value;

    /// <summary>
    /// Returns true if this entry matches a given slot (same dentist + service,
    /// slot start is within the patient's acceptable range).
    /// </summary>
    public bool MatchesSlot(Guid dentistId, Guid serviceId, DateTimeOffset slotStartUtc) =>
        DentistId == dentistId &&
        ServiceId == serviceId &&
        slotStartUtc >= EarliestStartUtc &&
        slotStartUtc <= LatestStartUtc;
}
