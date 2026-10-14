namespace Dentara.Domain.Catalog;

using Dentara.Domain.Common;

/// <summary>
/// Declares that a service requires a specific type of resource with a minimum quantity.
///
/// Examples:
/// - "Root canal" requires 1 DentalUnit
/// - "Surgery" requires 1 Room + 1 DentalUnit + 1 SupportStaff
/// - "Panoramic X-ray" requires 1 Equipment (X-ray machine)
///
/// At booking time, the system finds concrete ClinicResources matching the type
/// requirement and allocates them within the appointment transaction (ADR-003).
///
/// The <see cref="QuantityRequired"/> field supports cases like "needs 2 assistants".
/// </summary>
public sealed class ResourceServiceRequirement : Entity
{
    public Guid ServiceId { get; private set; }

    /// <summary>
    /// The required resource type. The system will look for any active
    /// ClinicResource with this TypeValue to satisfy the requirement.
    /// </summary>
    public int TypeValue { get; private set; }

    public ResourceType ResourceType => ResourceType.FromValue(TypeValue);

    /// <summary>
    /// How many resources of this type are required concurrently.
    /// </summary>
    public int QuantityRequired { get; private set; }

    /// <summary>
    /// If true, only resources with OwnerDentistId = the booking's dentist
    /// are eligible. Useful for personal dental chairs.
    /// </summary>
    public bool RequireDentistOwned { get; private set; }

    /// <summary>
    /// Optional: if set, only this specific resource satisfies the requirement.
    /// Useful when a service needs a specific named X-ray machine.
    /// </summary>
    public Guid? SpecificResourceId { get; private set; }

    private ResourceServiceRequirement() { } // EF Core

    public static ResourceServiceRequirement Create(
        Guid serviceId,
        ResourceType resourceType,
        int quantityRequired,
        IClock clock,
        bool requireDentistOwned = false,
        Guid? specificResourceId = null)
    {
        if (serviceId == Guid.Empty)
            throw new ArgumentException("ServiceId is required.", nameof(serviceId));
        if (quantityRequired < 1)
            throw new ArgumentException("Quantity required must be at least 1.", nameof(quantityRequired));
        if (specificResourceId.HasValue && specificResourceId.Value == Guid.Empty)
            throw new ArgumentException("SpecificResourceId cannot be empty.", nameof(specificResourceId));
        if (specificResourceId.HasValue && requireDentistOwned)
            throw new ArgumentException(
                "Cannot require both a specific resource and dentist-owned.", nameof(specificResourceId));

        return new ResourceServiceRequirement
        {
            ServiceId = serviceId,
            TypeValue = resourceType.Value,
            QuantityRequired = quantityRequired,
            RequireDentistOwned = requireDentistOwned,
            SpecificResourceId = specificResourceId,
            CreatedAtUtc = clock.UtcNow
        };
    }

    public void UpdateQuantity(int newQuantity, IClock clock)
    {
        if (newQuantity < 1)
            throw new ArgumentException("Quantity required must be at least 1.", nameof(newQuantity));
        QuantityRequired = newQuantity;
        Touch(clock.UtcNow);
    }
}
