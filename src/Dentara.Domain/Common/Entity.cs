namespace Dentara.Domain.Common;

/// <summary>
/// Base class for all entities in the system. Provides identity and audit fields.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAtUtc { get; protected set; }

    public DateTimeOffset? UpdatedAtUtc { get; protected set; }

    /// <summary>
    /// Concurrency token for optimistic locking. EF Core maps this to a rowversion column.
    /// </summary>
    public byte[] RowVersion { get; protected set; } = Array.Empty<byte>();

    protected void Touch(DateTimeOffset nowUtc)
    {
        UpdatedAtUtc = nowUtc;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return Id == other.Id;
    }

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(Entity? left, Entity? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(Entity? left, Entity? right) => !(left == right);
}
