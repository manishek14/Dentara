namespace Dentara.Domain.Identity;

using Dentara.Domain.Common;

/// <summary>
/// Roles available in the system. Stored as a reference table; the int Value
/// is persisted in UserRoles.RoleValue (CHECK constraint enforces valid values).
/// </summary>
public sealed class Role : Enumeration
{
    public static readonly Role Patient = new(1, nameof(Patient));
    public static readonly Role Dentist = new(2, nameof(Dentist));
    public static readonly Role Receptionist = new(3, nameof(Receptionist));
    public static readonly Role Admin = new(4, nameof(Admin));

    private Role(int value, string name) : base(value, name) { }

    public static IEnumerable<Role> All() => GetAll<Role>();

    public static Role FromValue(int value) => FromValue<Role>(value);

    public static Role FromName(string name) => FromName<Role>(name);
}
