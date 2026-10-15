namespace Dentara.Domain.Exceptions.Catalog;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when a booking references a Service that is inactive or soft-deleted.
/// </summary>
public sealed class ServiceNotActiveException : DomainException
{
    public Guid ServiceId { get; }

    public ServiceNotActiveException(Guid serviceId)
        : base(
            "CATALOG_SERVICE_NOT_ACTIVE",
            $"Service {serviceId} is not active.",
            serviceId)
    {
        ServiceId = serviceId;
    }
}
