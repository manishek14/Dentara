namespace Dentara.Domain.Payments;

using Dentara.Domain.Common;

/// <summary>
/// State of a single PaymentAttempt. Per ADR-004, this is INDEPENDENT of
/// AppointmentStatus. The two state machines synchronize at well-defined points.
///
/// Critical: receiving a callback does NOT automatically mean Succeeded.
/// The Application layer must verify the callback signature and explicitly
/// transition via MarkSucceeded or MarkFailed.
/// </summary>
public sealed class PaymentStatus : Enumeration
{
    /// <summary>
    /// Payment attempt created, user has not yet been redirected to gateway.
    /// </summary>
    public static readonly PaymentStatus Initiated = new(1, nameof(Initiated));

    /// <summary>
    /// User has been redirected to the gateway, awaiting callback.
    /// </summary>
    public static readonly PaymentStatus Pending = new(2, nameof(Pending));

    /// <summary>
    /// Gateway confirmed success via signed callback or direct verify call.
    /// </summary>
    public static readonly PaymentStatus Succeeded = new(3, nameof(Succeeded));

    /// <summary>
    /// Gateway confirmed failure. Terminal state (no refund possible).
    /// </summary>
    public static readonly PaymentStatus Failed = new(4, nameof(Failed));

    /// <summary>
    /// Gateway did not respond or response was ambiguous. The Application
    /// layer must verify with the gateway before transitioning to a terminal
    /// state. NEVER treat Unknown as Failed (ADR-004).
    /// </summary>
    public static readonly PaymentStatus Unknown = new(5, nameof(Unknown));

    /// <summary>
    /// Refund has been requested but not yet confirmed by the gateway.
    /// </summary>
    public static readonly PaymentStatus RefundPending = new(6, nameof(RefundPending));

    /// <summary>
    /// Partial refund confirmed. Further refunds are still possible.
    /// </summary>
    public static readonly PaymentStatus PartiallyRefunded = new(7, nameof(PartiallyRefunded));

    /// <summary>
    /// Full refund confirmed. Terminal state.
    /// </summary>
    public static readonly PaymentStatus Refunded = new(8, nameof(Refunded));

    /// <summary>
    /// Refund request failed at the gateway. Requires manual intervention.
    /// </summary>
    public static readonly PaymentStatus RefundFailed = new(9, nameof(RefundFailed));

    private PaymentStatus(int value, string name) : base(value, name) { }

    public static IEnumerable<PaymentStatus> All() => GetAll<PaymentStatus>();

    public static PaymentStatus FromValue(int value) => FromValue<PaymentStatus>(value);

    public static PaymentStatus FromName(string name) => FromName<PaymentStatus>(name);

    /// <summary>
    /// True if no further state transitions are possible (terminal state).
    /// </summary>
    public bool IsTerminal =>
        this == Failed || this == Refunded;

    /// <summary>
    /// True if this status represents a successful payment that can be refunded.
    /// </summary>
    public bool CanBeRefunded =>
        this == Succeeded || this == PartiallyRefunded;

    /// <summary>
    /// True if this status represents an in-flight refund operation.
    /// </summary>
    public bool IsRefundInFlight =>
        this == RefundPending || this == RefundFailed;
}
