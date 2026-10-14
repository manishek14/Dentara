namespace Dentara.Domain.Booking;

using Dentara.Domain.Common;

/// <summary>
/// Type of appointment event, recorded in the audit trail.
/// Per ADR-004, every state transition is logged for dispute resolution
/// and financial audit.
/// </summary>
public sealed class AppointmentEventType : Enumeration
{
    public static readonly AppointmentEventType Created = new(1, nameof(Created));
    public static readonly AppointmentEventType PaymentWindowStarted = new(2, nameof(PaymentWindowStarted));
    public static readonly AppointmentEventType Confirmed = new(3, nameof(Confirmed));
    public static readonly AppointmentEventType Expired = new(4, nameof(Expired));
    public static readonly AppointmentEventType Cancelled = new(5, nameof(Cancelled));
    public static readonly AppointmentEventType Completed = new(6, nameof(Completed));
    public static readonly AppointmentEventType NoShow = new(7, nameof(NoShow));
    public static readonly AppointmentEventType ResourceAllocated = new(8, nameof(ResourceAllocated));
    public static readonly AppointmentEventType ResourceReleased = new(9, nameof(ResourceReleased));
    public static readonly AppointmentEventType RefundInitiated = new(10, nameof(RefundInitiated));
    public static readonly AppointmentEventType RefundCompleted = new(11, nameof(RefundCompleted));

    private AppointmentEventType(int value, string name) : base(value, name) { }

    public static IEnumerable<AppointmentEventType> All() => GetAll<AppointmentEventType>();

    public static AppointmentEventType FromValue(int value) => FromValue<AppointmentEventType>(value);

    public static AppointmentEventType FromName(string name) => FromName<AppointmentEventType>(name);
}
