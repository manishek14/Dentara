namespace Dentara.Domain.Booking;

using Dentara.Domain.Common;

/// <summary>
/// Lifecycle state of an appointment. Per ADR-004, this is INDEPENDENT of the
/// payment state machine. The two state machines interact only at well-defined
/// synchronization points (e.g. successful payment triggers PendingPayment→Confirmed).
/// </summary>
public sealed class AppointmentStatus : Enumeration
{
    /// <summary>
    /// Reservation created, awaiting payment. Consumes capacity until ExpiresAtUtc
    /// (10-minute window per architecture spec).
    /// </summary>
    public static readonly AppointmentStatus PendingPayment = new(1, nameof(PendingPayment));

    /// <summary>
    /// Payment confirmed. Capacity is reserved. Patient may cancel (triggers refund flow).
    /// </summary>
    public static readonly AppointmentStatus Confirmed = new(2, nameof(Confirmed));

    /// <summary>
    /// Payment window elapsed without successful payment. Capacity is released.
    /// Terminal state (cannot transition out).
    /// </summary>
    public static readonly AppointmentStatus Expired = new(3, nameof(Expired));

    /// <summary>
    /// Cancelled by patient or staff. Capacity is released. May have a refund in flight.
    /// Terminal state.
    /// </summary>
    public static readonly AppointmentStatus Cancelled = new(4, nameof(Cancelled));

    /// <summary>
    /// Service was delivered. Historical state; no capacity impact.
    /// Terminal state. Set by staff after the appointment time passes.
    /// </summary>
    public static readonly AppointmentStatus Completed = new(5, nameof(Completed));

    /// <summary>
    /// Patient did not show up. Historical state. May affect future booking eligibility.
    /// Terminal state.
    /// </summary>
    public static readonly AppointmentStatus NoShow = new(6, nameof(NoShow));

    private AppointmentStatus(int value, string name) : base(value, name) { }

    public static IEnumerable<AppointmentStatus> All() => GetAll<AppointmentStatus>();

    public static AppointmentStatus FromValue(int value) => FromValue<AppointmentStatus>(value);

    public static AppointmentStatus FromName(string name) => FromName<AppointmentStatus>(name);

    /// <summary>
    /// True if this status means the appointment is currently consuming capacity
    /// (i.e. has a live, active reservation).
    /// </summary>
    public bool ConsumesCapacity =>
        this == PendingPayment || this == Confirmed;

    /// <summary>
    /// True if this is a terminal state (no further transitions allowed).
    /// </summary>
    public bool IsTerminal =>
        this == Expired || this == Cancelled || this == Completed || this == NoShow;

    /// <summary>
    /// True if the appointment is in a historical state (time has passed).
    /// Used to exclude from active capacity queries.
    /// </summary>
    public bool IsHistorical =>
        this == Completed || this == NoShow;
}
