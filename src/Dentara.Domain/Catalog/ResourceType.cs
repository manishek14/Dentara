namespace Dentara.Domain.Catalog;

using Dentara.Domain.Common;

/// <summary>
/// Type of clinic resource. Stored as int in the database with a CHECK constraint.
/// </summary>
public sealed class ResourceType : Enumeration
{
    /// <summary>
    /// A dental chair/unit. Capacity is typically 1.
    /// </summary>
    public static readonly ResourceType DentalUnit = new(1, nameof(DentalUnit));

    /// <summary>
    /// A piece of equipment (e.g. X-ray machine, sterilizer). Capacity may be &gt; 1.
    /// </summary>
    public static readonly ResourceType Equipment = new(2, nameof(Equipment));

    /// <summary>
    /// A physical room (e.g. surgery room). Capacity is typically 1.
    /// </summary>
    public static readonly ResourceType Room = new(3, nameof(Room));

    /// <summary>
    /// A support staff member (e.g. assistant). Capacity is typically 1 per shift.
    /// </summary>
    public static readonly ResourceType SupportStaff = new(4, nameof(SupportStaff));

    private ResourceType(int value, string name) : base(value, name) { }

    public static IEnumerable<ResourceType> All() => GetAll<ResourceType>();

    public static ResourceType FromValue(int value) => FromValue<ResourceType>(value);

    public static ResourceType FromName(string name) => FromName<ResourceType>(name);
}
