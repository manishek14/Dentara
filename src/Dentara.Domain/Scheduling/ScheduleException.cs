namespace Dentara.Domain.Scheduling;

using Dentara.Domain.Common;

/// <summary>
/// Override of a dentist's regular WorkSchedule for a specific date range.
/// Examples: vacation Sep 1-15, holiday Oct 13, reduced hours tomorrow 12:00-16:00.
///
/// EffectiveRange is in UTC (per ADR-005). For "all day" exceptions, set
/// EffectiveFromUtc = start-of-day-clinic-local, EffectiveUntilUtc = end-of-day-clinic-local.
///
/// For ReducedHours, the dentist is available ONLY in the ReducedWindow during the range.
/// For AdditionalHours, the dentist is available ADDITIONALLY in the ReducedWindow
///   (semantically misnamed but kept simple: the window is the availability window).
/// For Unavailability and Holiday, ReducedWindow is null (entire range is unavailable).
/// </summary>
public sealed class ScheduleException : Entity
{
    public Guid DentistId { get; private set; }

    /// <summary>
    /// If null, this exception applies to ALL dentists (e.g. a clinic-wide holiday).
    /// </summary>
    public Guid? DentistIdFilter => DentistId == Guid.Empty ? null : DentistId;

    public int TypeValue { get; private set; }

    public ScheduleExceptionType Type => ScheduleExceptionType.FromValue(TypeValue);

    public DateTimeOffset EffectiveFromUtc { get; private set; }

    public DateTimeOffset EffectiveUntilUtc { get; private set; }

    /// <summary>
    /// For ReducedHours and AdditionalHours: the time window within each day
    /// of the EffectiveRange that the dentist is available.
    /// For Unavailability and Holiday: null.
    /// </summary>
    public TimeSpan? WindowStartTime { get; private set; }

    public TimeSpan? WindowEndTime { get; private set; }

    public string? Reason { get; private set; }

    private ScheduleException() { } // EF Core

    public static ScheduleException CreateUnavailability(
        Guid dentistId,
        DateTimeOffset effectiveFromUtc,
        DateTimeOffset effectiveUntilUtc,
        IClock clock,
        string? reason = null)
    {
        if (dentistId == Guid.Empty)
            throw new ArgumentException("DentistId is required.", nameof(dentistId));

        ValidateRange(effectiveFromUtc, effectiveUntilUtc);

        return new ScheduleException
        {
            DentistId = dentistId,
            TypeValue = ScheduleExceptionType.Unavailability.Value,
            EffectiveFromUtc = effectiveFromUtc,
            EffectiveUntilUtc = effectiveUntilUtc,
            Reason = reason,
            CreatedAtUtc = clock.UtcNow
        };
    }

    public static ScheduleException CreateHoliday(
        DateTimeOffset effectiveFromUtc,
        DateTimeOffset effectiveUntilUtc,
        IClock clock,
        string? reason = null)
    {
        ValidateRange(effectiveFromUtc, effectiveUntilUtc);

        // DentistId = Guid.Empty signals "all dentists"
        return new ScheduleException
        {
            DentistId = Guid.Empty,
            TypeValue = ScheduleExceptionType.Holiday.Value,
            EffectiveFromUtc = effectiveFromUtc,
            EffectiveUntilUtc = effectiveUntilUtc,
            Reason = reason,
            CreatedAtUtc = clock.UtcNow
        };
    }

    public static ScheduleException CreateReducedHours(
        Guid dentistId,
        DateTimeOffset effectiveFromUtc,
        DateTimeOffset effectiveUntilUtc,
        TimeSpan windowStartTime,
        TimeSpan windowEndTime,
        IClock clock,
        string? reason = null)
    {
        if (dentistId == Guid.Empty)
            throw new ArgumentException("DentistId is required.", nameof(dentistId));

        ValidateRange(effectiveFromUtc, effectiveUntilUtc);
        // Reuse DailyTimeWindow for validation
        var _ = new DailyTimeWindow(DayOfWeek.Monday, windowStartTime, windowEndTime);

        return new ScheduleException
        {
            DentistId = dentistId,
            TypeValue = ScheduleExceptionType.ReducedHours.Value,
            EffectiveFromUtc = effectiveFromUtc,
            EffectiveUntilUtc = effectiveUntilUtc,
            WindowStartTime = windowStartTime,
            WindowEndTime = windowEndTime,
            Reason = reason,
            CreatedAtUtc = clock.UtcNow
        };
    }

    public static ScheduleException CreateAdditionalHours(
        Guid dentistId,
        DateTimeOffset effectiveFromUtc,
        DateTimeOffset effectiveUntilUtc,
        TimeSpan windowStartTime,
        TimeSpan windowEndTime,
        IClock clock,
        string? reason = null)
    {
        if (dentistId == Guid.Empty)
            throw new ArgumentException("DentistId is required.", nameof(dentistId));

        ValidateRange(effectiveFromUtc, effectiveUntilUtc);
        var _ = new DailyTimeWindow(DayOfWeek.Monday, windowStartTime, windowEndTime);

        return new ScheduleException
        {
            DentistId = dentistId,
            TypeValue = ScheduleExceptionType.AdditionalHours.Value,
            EffectiveFromUtc = effectiveFromUtc,
            EffectiveUntilUtc = effectiveUntilUtc,
            WindowStartTime = windowStartTime,
            WindowEndTime = windowEndTime,
            Reason = reason,
            CreatedAtUtc = clock.UtcNow
        };
    }

    /// <summary>
    /// Returns true if this exception is active at the given UTC instant.
    /// </summary>
    public bool IsActiveAt(DateTimeOffset utcInstant) =>
        utcInstant >= EffectiveFromUtc && utcInstant < EffectiveUntilUtc;

    /// <summary>
    /// Returns true if this exception overlaps with the given UTC time range.
    /// Half-open on both ends.
    /// </summary>
    public bool OverlapsRange(DateTimeOffset rangeStartUtc, DateTimeOffset rangeEndUtc) =>
        EffectiveFromUtc < rangeEndUtc && rangeStartUtc < EffectiveUntilUtc;

    private static void ValidateRange(DateTimeOffset fromUtc, DateTimeOffset untilUtc)
    {
        if (fromUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("From must be UTC.", nameof(fromUtc));
        if (untilUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Until must be UTC.", nameof(untilUtc));
        if (fromUtc >= untilUtc)
            throw new ArgumentException("From must be before Until.");
    }
}
