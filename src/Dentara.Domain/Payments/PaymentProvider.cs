namespace Dentara.Domain.Payments;

using Dentara.Domain.Common;

/// <summary>
/// Identifies which payment gateway provider is being used.
/// Stored as int with CHECK constraint. Useful for routing callbacks
/// and for splitting per-provider idempotency keys.
/// </summary>
public sealed class PaymentProvider : Enumeration
{
    /// <summary>
    /// Sandbox provider for development/testing. Simulates callbacks.
    /// </summary>
    public static readonly PaymentProvider Sandbox = new(1, nameof(Sandbox));

    /// <summary>
    /// Zarinpal Iranian payment gateway.
    /// </summary>
    public static readonly PaymentProvider Zarinpal = new(2, nameof(Zarinpal));

    /// <summary>
    /// Sadad (Behpardakht) Iranian payment gateway.
    /// </summary>
    public static readonly PaymentProvider Sadad = new(3, nameof(Sadad));

    /// <summary>
    /// Sep (Saman E-Payment) Iranian payment gateway.
    /// </summary>
    public static readonly PaymentProvider Sep = new(4, nameof(Sep));

    private PaymentProvider(int value, string name) : base(value, name) { }

    public static IEnumerable<PaymentProvider> All() => GetAll<PaymentProvider>();

    public static PaymentProvider FromValue(int value) => FromValue<PaymentProvider>(value);

    public static PaymentProvider FromName(string name) => FromName<PaymentProvider>(name);
}
