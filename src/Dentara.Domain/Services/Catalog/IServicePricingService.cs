namespace Dentara.Domain.Services.Catalog;

using Dentara.Domain.Catalog;

/// <summary>
/// Domain service for resolving the effective price and duration of a service
/// for a specific dentist. Encapsulates the DentistService override logic.
/// Used by the booking flow to compute snapshot values (ADR-007).
/// </summary>
public interface IServicePricingService
{
    /// <summary>
    /// Returns the effective price and duration for the given service + dentist.
    /// Falls back to Service base values if no DentistService override exists.
    /// Throws ServiceNotActiveException if either Service or DentistService is inactive.
    /// </summary>
    Task<ServicePricing> ResolveAsync(
        Guid serviceId,
        Guid dentistId,
        CancellationToken cancellationToken = default);
}

public sealed record ServicePricing(
    Guid ServiceId,
    string ServiceName,
    int DurationMinutes,
    decimal PriceAmount,
    string Currency);
