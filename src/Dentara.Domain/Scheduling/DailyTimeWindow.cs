namespace Dentara.Domain.Scheduling;

using Dentara.Domain.Common;

/// <summary>
/// Represents a daily working time window in the clinic's local timezone.
/// Stored as TimeSpan (not DateTimeOffset) because it is recurring weekly.
///
/// Example: a dentist works Saturdays 09:00-12:00 and 16:00-20:00.
/// That's two WorkSchedule rows, both with DayOfWeek=Saturday.
///
/// Half-open: [StartTime, EndTime). Two windows on the same day must not overlap.
/// </summary>
public readonly record struct DailyTimeWindow
{
    public DayOfWeek DayOfWeek { get; init; }

    public TimeSpan StartTime { get; init; }

    public TimeSpan EndTime { get; init; }

    public DailyTimeWindow(DayOfWeek dayOfWeek, TimeSpan startTime, TimeSpan endTime)
    {
        if (startTime >= endTime)
            throw new ArgumentException("Start must be before end.", nameof(startTime));
        if (startTime < TimeSpan.Zero || startTime >= TimeSpan.FromHours(24))
            throw new ArgumentException("Start must be in [00:00, 24:00).", nameof(startTime));
        if (endTime <= TimeSpan.Zero || endTime > TimeSpan.FromHours(24))
            throw new ArgumentException("End must be in (00:00, 24:00].", nameof(endTime));

        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
    }

    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Returns true if this window overlaps with <paramref name="other"/> on the
    /// same day. Windows on different days never overlap.
    /// </summary>
    public bool Overlaps(DailyTimeWindow other) =>
        DayOfWeek == other.DayOfWeek &&
        StartTime < other.EndTime && other.StartTime < EndTime;

    public override string ToString() => $"{DayOfWeek} {StartTime:hh\\:mm}-{EndTime:hh\\:mm}";
}
