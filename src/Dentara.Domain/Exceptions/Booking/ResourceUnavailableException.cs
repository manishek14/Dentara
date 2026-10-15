namespace Dentara.Domain.Exceptions.Booking;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when a required resource is unavailable for the requested time range.
/// Example: dental unit #1 is already allocated to another booking.
///
/// Distinct from SlotUnavailableException: this is raised when the dentist is
/// available but a specific resource (equipment, room, assistant) is not.
/// </summary>
public sealed class ResourceUnavailableException : DomainException
{
    public Guid ResourceId { get; }
    public DateTimeOffset RequestedStartUtc { get; }
    public DateTimeOffset RequestedEndUtc { get; }

    public ResourceUnavailableException(Guid resourceId, DateTimeOffset startUtc, DateTimeOffset endUtc)
        : base(
            "BOOKING_RESOURCE_UNAVAILABLE",
            $"Resource {resourceId} is not available for [{startUtc:O}, {endUtc:O}).")
    {
        ResourceId = resourceId;
        RequestedStartUtc = startUtc;
        RequestedEndUtc = endUtc;
    }
}
