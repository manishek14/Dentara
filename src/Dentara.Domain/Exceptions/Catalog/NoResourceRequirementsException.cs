namespace Dentara.Domain.Exceptions.Catalog;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when a Service has no ResourceServiceRequirements defined, meaning
/// no resources would be allocated. The booking flow requires at least one
/// (even if just the dentist as a resource, though the dentist is tracked
/// separately on the Appointment).
/// </summary>
public sealed class NoResourceRequirementsException : DomainException
{
    public Guid ServiceId { get; }

    public NoResourceRequirementsException(Guid serviceId)
        : base(
            "CATALOG_NO_RESOURCE_REQUIREMENTS",
            $"Service {serviceId} has no resource requirements defined. " +
            "At least one is required for booking.",
            serviceId)
    {
        ServiceId = serviceId;
    }
}
