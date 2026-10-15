namespace Dentara.Domain.Exceptions.Booking;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when a booking operation cannot proceed because the requested time
/// slot is no longer available (overlap detected, capacity exhausted).
///
/// This is the EXPECTED outcome under concurrent load: per ADR-003, when 100
/// requests compete for 10 slots, 90 of them will receive this exception.
/// The API layer should map this to HTTP 409 Conflict.
/// </summary>
public sealed class SlotUnavailableException : DomainException
{
    public Guid DentistId { get; }
    public DateTimeOffset RequestedStartUtc { get; }
    public DateTimeOffset RequestedEndUtc { get; }

    public SlotUnavailableException(Guid dentistId, DateTimeOffset startUtc, DateTimeOffset endUtc)
        : base(
            "BOOKING_SLOT_UNAVAILABLE",
            $"The requested slot [{startUtc:O}, {endUtc:O}) for dentist {dentistId} is no longer available.")
    {
        DentistId = dentistId;
        RequestedStartUtc = startUtc;
        RequestedEndUtc = endUtc;
    }
}
