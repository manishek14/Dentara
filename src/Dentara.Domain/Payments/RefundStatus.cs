namespace Dentara.Domain.Payments;

using Dentara.Domain.Common;

/// <summary>
/// Status of a refund operation against a PaymentAttempt.
/// </summary>
public sealed class RefundStatus : Enumeration
{
    /// <summary>
    /// Refund has been requested but not yet confirmed by the gateway.
    /// </summary>
    public static readonly RefundStatus Pending = new(1, nameof(Pending));

    /// <summary>
    /// Gateway confirmed the refund. Terminal state.
    /// </summary>
    public static readonly RefundStatus Succeeded = new(2, nameof(Succeeded));

    /// <summary>
    /// Gateway rejected the refund. Requires manual intervention.
    /// </summary>
    public static readonly RefundStatus Failed = new(3, nameof(Failed));

    private RefundStatus(int value, string name) : base(value, name) { }

    public static IEnumerable<RefundStatus> All() => GetAll<RefundStatus>();

    public static RefundStatus FromValue(int value) => FromValue<RefundStatus>(value);

    public static RefundStatus FromName(string name) => FromName<RefundStatus>(name);

    public bool IsTerminal => this == Succeeded || this == Failed;
}
