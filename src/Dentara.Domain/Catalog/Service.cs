namespace Dentara.Domain.Catalog;

using Dentara.Domain.Common;

/// <summary>
/// A billable dental service (e.g. "Cavity filling", "Root canal", "Teeth cleaning").
/// Holds the base duration and price. The actual price and duration for a specific
/// booking is snapshotted on the Appointment row (ADR-007).
///
/// A service is soft-deletable (IsActive=false) so that historical appointments
/// continue to reference a valid ServiceId, but new bookings cannot use it.
/// </summary>
public sealed class Service : Entity
{
    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>
    /// Base duration in minutes. Per-dentist override is allowed via DentistService.
    /// </summary>
    public int DurationMinutes { get; private set; }

    /// <summary>
    /// Base price. Per-dentist override is allowed via DentistService.
    /// </summary>
    public decimal PriceAmount { get; private set; }

    public string Currency { get; private set; } = "IRR";

    public bool IsActive { get; private set; }

    /// <summary>
    /// Version of this service definition, incremented on each breaking change
    /// (price or duration). Used by the audit log to correlate which version
    /// a snapshot was taken from.
    /// </summary>
    public int Version { get; private set; }

    private readonly List<DentistService> _dentistServices = new();
    public IReadOnlyCollection<DentistService> DentistServices => _dentistServices.AsReadOnly();

    private readonly List<ResourceServiceRequirement> _resourceRequirements = new();
    public IReadOnlyCollection<ResourceServiceRequirement> ResourceRequirements =>
        _resourceRequirements.AsReadOnly();

    private Service() { } // EF Core

    public static Service Create(
        string name,
        int durationMinutes,
        decimal priceAmount,
        string currency,
        IClock clock,
        string? description = null)
    {
        ValidateName(name);
        ValidateDuration(durationMinutes);
        ValidatePrice(priceAmount);
        var currencyCode = ValidateCurrency(currency);

        return new Service
        {
            Name = name.Trim(),
            Description = description,
            DurationMinutes = durationMinutes,
            PriceAmount = priceAmount,
            Currency = currencyCode,
            IsActive = true,
            Version = 1,
            CreatedAtUtc = clock.UtcNow
        };
    }

    public void UpdateBaseTerms(
        int durationMinutes,
        decimal priceAmount,
        string currency,
        IClock clock)
    {
        ValidateDuration(durationMinutes);
        ValidatePrice(priceAmount);
        var currencyCode = ValidateCurrency(currency);

        if (DurationMinutes != durationMinutes ||
            PriceAmount != priceAmount ||
            !string.Equals(Currency, currencyCode, StringComparison.OrdinalIgnoreCase))
        {
            // Breaking change: bump version so audit can correlate snapshots
            Version++;
        }

        DurationMinutes = durationMinutes;
        PriceAmount = priceAmount;
        Currency = currencyCode;
        Touch(clock.UtcNow);
    }

    public void UpdateMetadata(string name, string? description, IClock clock)
    {
        ValidateName(name);
        Name = name.Trim();
        Description = description;
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

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Service name is required.", nameof(name));
        if (name.Length > 200)
            throw new ArgumentException("Service name must be at most 200 characters.", nameof(name));
    }

    private static void ValidateDuration(int durationMinutes)
    {
        if (durationMinutes <= 0)
            throw new ArgumentException("Duration must be positive.", nameof(durationMinutes));
        if (durationMinutes > 24 * 60)
            throw new ArgumentException("Duration cannot exceed 24 hours.", nameof(durationMinutes));
    }

    private static void ValidatePrice(decimal priceAmount)
    {
        if (priceAmount < 0)
            throw new ArgumentException("Price must be non-negative.", nameof(priceAmount));
    }

    private static string ValidateCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be exactly 3 characters (ISO 4217).", nameof(currency));
        return currency.ToUpperInvariant();
    }
}
