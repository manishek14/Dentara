namespace Dentara.Domain.Exceptions.Scheduling;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when a BookingWindow publication worker fails to publish sessions
/// (e.g. due to overlapping windows or invalid dentist schedules).
/// </summary>
public sealed class BookingWindowPublicationException : DomainException
{
    public Guid BookingWindowId { get; }

    public BookingWindowPublicationException(Guid bookingWindowId, string reason)
        : base(
            "SCHEDULING_PUBLICATION_FAILED",
            $"Failed to publish booking window {bookingWindowId}: {reason}",
            bookingWindowId)
    {
        BookingWindowId = bookingWindowId;
    }
}
