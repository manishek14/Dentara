namespace Dentara.Domain.Catalog;

using Dentara.Domain.Common;

/// <summary>
/// Many-to-many between DentistProfile and Service. Captures whether a specific
/// dentist offers a service and optionally overrides the base duration/price.
///
/// UNIQUE(DentistId, ServiceId) prevents duplicate rows.
/// </summary>
public sealed class DentistService : Entity
{
    public Guid DentistId { get; private set; }

    public Guid ServiceId { get; private set; }

    /// <summary>
    /// If null, inherits Service.DurationMinutes. Otherwise overrides per-dentist.
    /// </summary>
    public int? DurationMinutesOverride { get; private set; }

    /// <summary>
    /// If null, inherits Service.PriceAmount/Currency. Otherwise overrides per-dentist.
    /// </summary>
    public decimal? PriceAmountOverride { get; private set; }

    public string? CurrencyOverride { get; private set; }

    public bool IsActive { get; private set; }

    private DentistService() { } // EF Core

    public static DentistService Create(
        Guid dentistId,
        Guid serviceId,
        IClock clock,
        int? durationMinutesOverride = null,
        decimal? priceAmountOverride = null,
        string? currencyOverride = null)
    {
        if (dentistId == Guid.Empty)
            throw new ArgumentException("DentistId is required.", nameof(dentistId));
        if (serviceId == Guid.Empty)
            throw new ArgumentException("ServiceId is required.", nameof(serviceId));

        ValidateOverrides(durationMinutesOverride, priceAmountOverride, currencyOverride);

        return new DentistService
        {
            DentistId = dentistId,
            ServiceId = serviceId,
            DurationMinutesOverride = durationMinutesOverride,
            PriceAmountOverride = priceAmountOverride,
            CurrencyOverride = currencyOverride,
            IsActive = true,
            CreatedAtUtc = clock.UtcNow
        };
    }

    public void UpdateOverrides(
        IClock clock,
        int? durationMinutesOverride = null,
        decimal? priceAmountOverride = null,
        string? currencyOverride = null)
    {
        ValidateOverrides(durationMinutesOverride, priceAmountOverride, currencyOverride);
        DurationMinutesOverride = durationMinutesOverride;
        PriceAmountOverride = priceAmountOverride;
        CurrencyOverride = currencyOverride;
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
    /// Resolves the effective duration: override or fall back to the base service duration.
    /// </summary>
    public int ResolveDuration(int baseServiceDuration) =>
        DurationMinutesOverride ?? baseServiceDuration;

    /// <summary>
    /// Resolves the effective price: override or fall back to the base service price.
    /// </summary>
    public (decimal Amount, string Currency) ResolvePrice(decimal baseAmount, string baseCurrency)
    {
        if (PriceAmountOverride.HasValue && !string.IsNullOrEmpty(CurrencyOverride))
            return (PriceAmountOverride.Value, CurrencyOverride);
        if (PriceAmountOverride.HasValue)
            return (PriceAmountOverride.Value, baseCurrency);
        return (baseAmount, baseCurrency);
    }

    private static void ValidateOverrides(
        int? durationMinutesOverride,
        decimal? priceAmountOverride,
        string? currencyOverride)
    {
        if (durationMinutesOverride.HasValue)
        {
            if (durationMinutesOverride.Value <= 0)
                throw new ArgumentException("Duration override must be positive.", nameof(durationMinutesOverride));
            if (durationMinutesOverride.Value > 24 * 60)
                throw new ArgumentException("Duration override cannot exceed 24 hours.", nameof(durationMinutesOverride));
        }

        if (priceAmountOverride.HasValue && priceAmountOverride.Value < 0)
            throw new ArgumentException("Price override must be non-negative.", nameof(priceAmountOverride));

        // Currency override is required if price override is set; otherwise it must be null.
        if (priceAmountOverride.HasValue && string.IsNullOrWhiteSpace(currencyOverride))
            throw new ArgumentException("Currency override is required when price override is set.", nameof(currencyOverride));
        if (!priceAmountOverride.HasValue && !string.IsNullOrWhiteSpace(currencyOverride))
            throw new ArgumentException("Currency override cannot be set without price override.", nameof(currencyOverride));

        if (!string.IsNullOrWhiteSpace(currencyOverride) && currencyOverride.Length != 3)
            throw new ArgumentException("Currency override must be 3 characters (ISO 4217).", nameof(currencyOverride));
    }
}
