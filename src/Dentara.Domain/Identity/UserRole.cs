namespace Dentara.Domain.Identity;

using Dentara.Domain.Common;

/// <summary>
/// Join table between User and Role. The RoleValue column stores the int Value
/// of the Role enumeration; a CHECK constraint restricts it to valid values.
/// A UNIQUE(UserId, RoleValue) constraint prevents duplicate role assignments.
/// </summary>
public sealed class UserRole : Entity
{
    public Guid UserId { get; private set; }

    /// <summary>
    /// Stored as int (Role.Value) instead of FK to Roles table.
    /// The Roles table is a reference table; CHECK constraint enforces validity.
    /// </summary>
    public int RoleValue { get; private set; }

    public DateTimeOffset ExpiresAtUtc { get; private set; }

    public bool IsExpired(IClock clock) => ExpiresAtUtc != DateTimeOffset.MaxValue && clock.UtcNow > ExpiresAtUtc;

    private UserRole() { } // EF Core

    internal static UserRole Create(Guid userId, Role role, IClock clock)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));

        return new UserRole
        {
            UserId = userId,
            RoleValue = role.Value,
            ExpiresAtUtc = DateTimeOffset.MaxValue, // no expiry by default
            CreatedAtUtc = clock.UtcNow
        };
    }

    internal static UserRole CreateWithExpiry(Guid userId, Role role, DateTimeOffset expiresAtUtc, IClock clock)
    {
        var ur = Create(userId, role, clock);
        if (expiresAtUtc <= clock.UtcNow)
            throw new ArgumentException("Expiry must be in the future.", nameof(expiresAtUtc));
        ur.ExpiresAtUtc = expiresAtUtc;
        return ur;
    }
}
