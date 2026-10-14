namespace Dentara.Domain.Scheduling;

using Dentara.Domain.Common;

/// <summary>
/// Status of a booking window (the publication gate).
///
/// A booking window defines WHEN patients can start booking sessions for a
/// specific date range. Example: "every Thursday at 17:00, slots for next
/// week become available".
/// </summary>
public sealed class BookingWindowStatus : Enumeration
{
    /// <summary>
    /// Window is created but not yet open. No sessions are published yet.
    /// </summary>
    public static readonly BookingWindowStatus Scheduled = new(1, nameof(Scheduled));

    /// <summary>
    /// Window is open. The publication worker has generated PublishedSessions
    /// for this window. Patients can book.
    /// </summary>
    public static readonly BookingWindowStatus Open = new(2, nameof(Open));

    /// <summary>
    /// Window has closed (OpensAtUtc + Duration has passed). No new sessions
    /// can be booked from this window, but existing sessions remain bookable
    /// until their own start time.
    /// </summary>
    public static readonly BookingWindowStatus Closed = new(3, nameof(Closed));

    /// <summary>
    /// Window was cancelled by an admin (e.g. misconfigured). Sessions that
    /// were never booked are removed; sessions that were booked remain.
    /// </summary>
    public static readonly BookingWindowStatus Cancelled = new(4, nameof(Cancelled));

    private BookingWindowStatus(int value, string name) : base(value, name) { }

    public static IEnumerable<BookingWindowStatus> All() => GetAll<BookingWindowStatus>();

    public static BookingWindowStatus FromValue(int value) => FromValue<BookingWindowStatus>(value);

    public static BookingWindowStatus FromName(string name) => FromName<BookingWindowStatus>(name);
}
