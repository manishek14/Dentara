namespace Dentara.Domain.Catalog;

using Dentara.Domain.Common;

/// <summary>
/// A physical/logical resource of the clinic that can be allocated to an appointment.
/// Examples: dental chair #1, X-ray machine, surgery room, assistant on duty.
///
/// Capacity represents how many concurrent allocations are allowed. For a single
/// dental unit, Capacity=1. For a shared sterilizer, Capacity may be &gt; 1.
///
/// A resource is soft-deletable so historical allocations still reference a valid Id.
/// </summary>
public sealed class ClinicResource : Entity
{
    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int TypeValue { get; private set; }

    public ResourceType Type => ResourceType.FromValue(TypeValue);

    /// <summary>
    /// Maximum concurrent active allocations. Must be &gt;= 1.
    /// For ADR-003's overlap check, the system queries ResourceAllocations
    /// where COUNT(active) &lt; Capacity for the requested time range.
    /// </summary>
    public int Capacity { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>
    /// Optional: a specific dentist that "owns" this resource (e.g. their personal chair).
    /// If null, the resource is shared among all dentists.
    /// </summary>
    public Guid? OwnerDentistId { get; private set; }

    private readonly List<ResourceServiceRequirement> _serviceRequirements = new();
    public IReadOnlyCollection<ResourceServiceRequirement> ServiceRequirements =>
        _serviceRequirements.AsReadOnly();

    private ClinicResource() { } // EF Core

    public static ClinicResource Create(
        string name,
        ResourceType type,
        int capacity,
        IClock clock,
        string? description = null,
        Guid? ownerDentistId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Resource name is required.", nameof(name));
        if (name.Length > 200)
            throw new ArgumentException("Resource name must be at most 200 characters.", nameof(name));
        if (capacity < 1)
            throw new ArgumentException("Capacity must be at least 1.", nameof(capacity));

        return new ClinicResource
        {
            Name = name.Trim(),
            Description = description,
            TypeValue = type.Value,
            Capacity = capacity,
            IsActive = true,
            OwnerDentistId = ownerDentistId,
            CreatedAtUtc = clock.UtcNow
        };
    }

    public void UpdateMetadata(string name, string? description, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Resource name is required.", nameof(name));
        if (name.Length > 200)
            throw new ArgumentException("Resource name must be at most 200 characters.", nameof(name));
        Name = name.Trim();
        Description = description;
        Touch(clock.UtcNow);
    }

    public void ChangeCapacity(int newCapacity, IClock clock)
    {
        if (newCapacity < 1)
            throw new ArgumentException("Capacity must be at least 1.", nameof(newCapacity));
        Capacity = newCapacity;
        Touch(clock.UtcNow);
    }

    public void AssignOwner(Guid dentistId, IClock clock)
    {
        if (dentistId == Guid.Empty)
            throw new ArgumentException("DentistId is required.", nameof(dentistId));
        OwnerDentistId = dentistId;
        Touch(clock.UtcNow);
    }

    public void ClearOwner(IClock clock)
    {
        OwnerDentistId = null;
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
}
