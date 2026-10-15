namespace Dentara.Domain.Exceptions.Booking;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when a booking operation is attempted outside the dentist's work
/// schedule, or during a schedule exception (vacation, holiday).
/// </summary>
public sealed class DentistNotAvailableException : DomainException
{
    public Guid DentistId { get; }
    public DateTimeOffset RequestedStartUtc { get; }
    public DateTimeOffset RequestedEndUtc { get; }

    public DentistNotAvailableException(Guid dentistId, DateTimeOffset startUtc, DateTimeOffset endUtc)
        : base(
            "BOOKING_DENTIST_NOT_AVAILABLE",
            $"Dentist {dentistId} is not available during [{startUtc:O}, {endUtc:O}).")
    {
        DentistId = dentistId;
        RequestedStartUtc = startUtc;
        RequestedEndUtc = endUtc;
    }
}
