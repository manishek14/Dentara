namespace Dentara.Domain.Scheduling;

using Dentara.Domain.Common;

/// <summary>
/// A booking window is the publication gate for a date range of sessions.
///
/// Example: a clinic that publishes next week's slots every Thursday at 17:00.
/// The BookingWindow row would be:
///   - OpensAtUtc = next Thursday 17:00 UTC (or clinic-local converted to UTC)
///   - ClosesAtUtc = next Thursday 17:30 UTC (30-min window for "early bird" effect,
///     or set to next Thursday 23:59 if always-open until start)
///   - ApplicableDateFromUtc = the Monday after next
///   - ApplicableDateUntilUtc = the Sunday after next
///   - Status = Scheduled (until the worker opens it)
///
/// A BackgroundWorker polls for BookingWindows where Status=Scheduled and
/// OpensAtUtc &lt;= now, then transitions them to Open and generates PublishedSessions.
///
/// Re-running the publication worker must be idempotent (ADR-006):
/// a UNIQUE(BookingWindowId, DentistId, StartAtUtc) on PublishedSessions
/// prevents duplicate session generation.
/// </summary>
public sealed class BookingWindow : Entity
{
    public DateTimeOffset OpensAtUtc { get; private set; }

    public DateTimeOffset ClosesAtUtc { get; private set; }

    /// <summary>
    /// The date range of sessions that this window publishes.
    /// A session whose StartAtUtc falls in [ApplicableDateFromUtc, ApplicableDateUntilUtc)
    /// is eligible for publication by this window.
    /// </summary>
    public DateTimeOffset ApplicableDateFromUtc { get; private set; }

    public DateTimeOffset ApplicableDateUntilUtc { get; private set; }

    public int StatusValue { get; private set; }

    public BookingWindowStatus Status => BookingWindowStatus.FromValue(StatusValue);

    /// <summary>
    /// If set, only this dentist's sessions are published by this window.
    /// If null, all active dentists' sessions are published.
    /// </summary>
    public Guid? DentistIdFilter { get; private set; }

    /// <summary>
    /// Free-text description for admins (e.g. "Nowruz holiday special").
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// When the publication worker actually ran. Null if not yet published.
    /// </summary>
    public DateTimeOffset? OpenedAtUtc { get; private set; }

    /// <summary>
    /// When the window was closed (either naturally or by admin).
    /// </summary>
    public DateTimeOffset? ClosedAtUtc { get; private set; }

    private readonly List<PublishedSession> _sessions = new();
    public IReadOnlyCollection<PublishedSession> Sessions => _sessions.AsReadOnly();

    private BookingWindow() { } // EF Core

    public static BookingWindow Create(
        DateTimeOffset opensAtUtc,
        DateTimeOffset closesAtUtc,
        DateTimeOffset applicableDateFromUtc,
        DateTimeOffset applicableDateUntilUtc,
        IClock clock,
        Guid? dentistIdFilter = null,
        string? description = null)
    {
        EnsureUtc(opensAtUtc, nameof(opensAtUtc));
        EnsureUtc(closesAtUtc, nameof(closesAtUtc));
        EnsureUtc(applicableDateFromUtc, nameof(applicableDateFromUtc));
        EnsureUtc(applicableDateUntilUtc, nameof(applicableDateUntilUtc));

        if (opensAtUtc >= closesAtUtc)
            throw new ArgumentException("OpensAt must be before ClosesAt.");
        if (applicableDateFromUtc >= applicableDateUntilUtc)
            throw new ArgumentException("ApplicableDateFrom must be before ApplicableDateUntil.");
        if (dentistIdFilter.HasValue && dentistIdFilter.Value == Guid.Empty)
            throw new ArgumentException("DentistIdFilter cannot be Guid.Empty (use null for all dentists).");

        return new BookingWindow
        {
            OpensAtUtc = opensAtUtc,
            ClosesAtUtc = closesAtUtc,
            ApplicableDateFromUtc = applicableDateFromUtc,
            ApplicableDateUntilUtc = applicableDateUntilUtc,
            StatusValue = BookingWindowStatus.Scheduled.Value,
            DentistIdFilter = dentistIdFilter,
            Description = description,
            CreatedAtUtc = clock.UtcNow
        };
    }

    public bool IsOpenAt(DateTimeOffset utcInstant) =>
        Status == BookingWindowStatus.Open ||
        (Status == BookingWindowStatus.Scheduled &&
         utcInstant >= OpensAtUtc && utcInstant < ClosesAtUtc);

    /// <summary>
    /// Transitions the window to Open. Idempotent: if already Open, no-op.
    /// Records OpenedAtUtc on first transition.
    /// </summary>
    public void MarkOpened(IClock clock)
    {
        if (Status == BookingWindowStatus.Open) return;
        if (Status != BookingWindowStatus.Scheduled)
            throw new InvalidOperationException(
                $"Cannot open a window in status {Status}.");

        StatusValue = BookingWindowStatus.Open.Value;
        OpenedAtUtc ??= clock.UtcNow;
        Touch(clock.UtcNow);
    }

    /// <summary>
    /// Transitions the window to Closed. Idempotent.
    /// </summary>
    public void MarkClosed(IClock clock)
    {
        if (Status == BookingWindowStatus.Closed) return;
        if (Status != BookingWindowStatus.Open)
            throw new InvalidOperationException(
                $"Cannot close a window in status {Status}.");

        StatusValue = BookingWindowStatus.Closed.Value;
        ClosedAtUtc ??= clock.UtcNow;
        Touch(clock.UtcNow);
    }

    public void Cancel(IClock clock)
    {
        if (Status == BookingWindowStatus.Cancelled) return;
        if (Status == BookingWindowStatus.Closed)
            throw new InvalidOperationException("Cannot cancel a closed window.");

        StatusValue = BookingWindowStatus.Cancelled.Value;
        ClosedAtUtc ??= clock.UtcNow;
        Touch(clock.UtcNow);
    }

    private static void EnsureUtc(DateTimeOffset value, string paramName)
    {
        if (value.Offset != TimeSpan.Zero)
            throw new ArgumentException("Value must be UTC.", paramName);
    }
}
