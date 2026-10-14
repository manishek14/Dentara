namespace Dentara.Domain.Scheduling;

using Dentara.Domain.Common;

/// <summary>
/// Recurring weekly work schedule for a dentist. Each row represents one
/// continuous work window on one day of the week, in the clinic's local timezone.
///
/// Example: Dr. Ahmadi works Sat/Mon/Wed 09:00-13:00 → 3 rows.
///
/// Work schedules are templates. They do NOT capture exceptions (holidays,
/// vacations) — those live in ScheduleException. They also do NOT capture
/// published sessions — those live in PublishedSession and are the actual
/// bookable slots.
/// </summary>
public sealed class WorkSchedule : Entity
{
    public Guid DentistId { get; private set; }

    /// <summary>
    /// Day of week in clinic's local timezone. Stored as int (DayOfWeek enum).
    /// </summary>
    public int DayOfWeekValue { get; private set; }

    public DayOfWeek DayOfWeek => (DayOfWeek)DayOfWeekValue;

    /// <summary>
    /// Start time in clinic's local timezone. Stored as TIME in SQL Server.
    /// </summary>
    public TimeSpan StartTime { get; private set; }

    public TimeSpan EndTime { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Optional validity window for this schedule entry. If set, the entry is
    /// only active between these UTC dates. Useful for seasonal schedules.
    /// </summary>
    public DateTimeOffset? EffectiveFromUtc { get; private set; }

    public DateTimeOffset? EffectiveUntilUtc { get; private set; }

    private WorkSchedule() { } // EF Core

    public DailyTimeWindow ToDailyTimeWindow() =>
        new(DayOfWeek, StartTime, EndTime);

    public static WorkSchedule Create(
        Guid dentistId,
        DayOfWeek dayOfWeek,
        TimeSpan startTime,
        TimeSpan endTime,
        IClock clock,
        DateTimeOffset? effectiveFromUtc = null,
        DateTimeOffset? effectiveUntilUtc = null)
    {
        if (dentistId == Guid.Empty)
            throw new ArgumentException("DentistId is required.", nameof(dentistId));

        // Validate via the value object to share logic
        var window = new DailyTimeWindow(dayOfWeek, startTime, endTime);

        if (effectiveFromUtc.HasValue && effectiveUntilUtc.HasValue &&
            effectiveFromUtc.Value >= effectiveUntilUtc.Value)
            throw new ArgumentException("EffectiveFrom must be before EffectiveUntil.");

        return new WorkSchedule
        {
            DentistId = dentistId,
            DayOfWeekValue = (int)dayOfWeek,
            StartTime = window.StartTime,
            EndTime = window.EndTime,
            IsActive = true,
            EffectiveFromUtc = effectiveFromUtc,
            EffectiveUntilUtc = effectiveUntilUtc,
            CreatedAtUtc = clock.UtcNow
        };
    }

    public void UpdateTimes(TimeSpan startTime, TimeSpan endTime, IClock clock)
    {
        var window = new DailyTimeWindow(DayOfWeek, startTime, endTime);
        StartTime = window.StartTime;
        EndTime = window.EndTime;
        Touch(clock.UtcNow);
    }

    public void SetValidityWindow(DateTimeOffset? fromUtc, DateTimeOffset? untilUtc, IClock clock)
    {
        if (fromUtc.HasValue && untilUtc.HasValue && fromUtc.Value >= untilUtc.Value)
            throw new ArgumentException("From must be before Until.");
        EffectiveFromUtc = fromUtc;
        EffectiveUntilUtc = untilUtc;
        Touch(clock.UtcNow);
    }

    public void Deactivate(IClock clock)
    {
        IsActive = false;
        Touch(clock.UtcNow);
    }

    public void Activate(IClock clock)
    {
        IsActive = true;
        Touch(clock.UtcNow);
    }

    /// <summary>
    /// Returns true if this schedule entry is effective at the given UTC instant.
    /// </summary>
    public bool IsEffectiveAt(DateTimeOffset utcInstant)
    {
        if (!IsActive) return false;
        if (EffectiveFromUtc.HasValue && utcInstant < EffectiveFromUtc.Value) return false;
        if (EffectiveUntilUtc.HasValue && utcInstant >= EffectiveUntilUtc.Value) return false;
        return true;
    }
}
