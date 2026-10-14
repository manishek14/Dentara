namespace Dentara.Domain.Common;

/// <summary>
/// Represents an immutable monetary amount with currency (ISO 4217).
/// Stored as two columns in SQL Server (Amount, Currency) via EF Core owned entity configuration.
/// </summary>
public readonly record struct Money
{
    public decimal Amount { get; init; }

    /// <summary>
    /// ISO 4217 currency code, e.g. "IRR", "USD", "EUR".
    /// </summary>
    public string Currency { get; init; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be non-negative.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency code is required.", nameof(currency));
        if (currency.Length != 3)
            throw new ArgumentException("Currency code must be exactly 3 characters (ISO 4217).", nameof(currency));

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = Amount + other.Amount };
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        var result = Amount - other.Amount;
        if (result < 0)
            throw new InvalidOperationException("Subtraction would result in a negative amount.");
        return this with { Amount = result };
    }

    public Money Multiply(decimal factor)
    {
        if (factor < 0)
            throw new ArgumentOutOfRangeException(nameof(factor), "Factor must be non-negative.");
        return this with { Amount = Amount * factor };
    }

    public Money ApplyPercent(decimal percent)
    {
        if (percent < 0 || percent > 100)
            throw new ArgumentOutOfRangeException(nameof(percent), "Percent must be in [0, 100].");
        return this with { Amount = Amount * (percent / 100m) };
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Cannot combine money with different currencies: {Currency} vs {other.Currency}.");
    }

    public override string ToString() => $"{Amount:N0} {Currency}";
}
