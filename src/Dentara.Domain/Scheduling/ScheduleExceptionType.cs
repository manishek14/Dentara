namespace Dentara.Domain.Scheduling;

using Dentara.Domain.Common;

/// <summary>
/// Type of schedule exception. Different types affect booking differently.
/// </summary>
public sealed class ScheduleExceptionType : Enumeration
{
    /// <summary>
    /// Dentist is unavailable (vacation, sick, conference). All sessions in
    /// the range are removed from publication; existing confirmed bookings
    /// must be manually rescheduled.
    /// </summary>
    public static readonly ScheduleExceptionType Unavailability = new(1, nameof(Unavailability));

    /// <summary>
    /// A public holiday. Affects all dentists. Could be auto-imported from a
    /// national holiday calendar in the future.
    /// </summary>
    public static readonly ScheduleExceptionType Holiday = new(2, nameof(Holiday));

    /// <summary>
    /// Reduced hours (e.g. half-day before a holiday). The dentist works
    /// only part of the regular schedule.
    /// </summary>
    public static readonly ScheduleExceptionType ReducedHours = new(3, nameof(ReducedHours));

    /// <summary>
    /// Extra hours — dentist is available outside regular schedule (e.g. an
    /// emergency shift on a Friday).
    /// </summary>
    public static readonly ScheduleExceptionType AdditionalHours = new(4, nameof(AdditionalHours));

    private ScheduleExceptionType(int value, string name) : base(value, name) { }

    public static IEnumerable<ScheduleExceptionType> All() => GetAll<ScheduleExceptionType>();

    public static ScheduleExceptionType FromValue(int value) => FromValue<ScheduleExceptionType>(value);

    public static ScheduleExceptionType FromName(string name) => FromName<ScheduleExceptionType>(name);
}
