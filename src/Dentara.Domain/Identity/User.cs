namespace Dentara.Domain.Identity;

using Dentara.Domain.Common;

/// <summary>
/// User account. Separate from PatientProfile and DentistProfile so that
/// one identity can hold multiple roles (e.g. a dentist who is also a patient).
///
/// The phone number is the primary identifier (OTP-based login). Password
/// is optional (set only if the user opts in to password-based login).
/// </summary>
public sealed class User : Entity
{
    /// <summary>
    /// Phone number in E.164 format, e.g. "+989121234567".
    /// </summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Normalized phone number for the UNIQUE index.
    /// Rule: strip spaces, lowercase, ensure leading "+".
    /// </summary>
    public string PhoneNumberNormalized { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public bool IsPhoneVerified { get; private set; }

    /// <summary>
    /// Optional password hash (BCrypt). Null if user uses OTP-only login.
    /// </summary>
    public string? PasswordHash { get; private set; }

    public DateTimeOffset? LastLoginAtUtc { get; private set; }

    public DateTimeOffset? PhoneVerifiedAtUtc { get; private set; }

    // Navigation (same module)
    private readonly List<UserRole> _roles = new();
    public IReadOnlyCollection<UserRole> Roles => _roles.AsReadOnly();

    private readonly List<PatientProfile> _patientProfiles = new();
    public IReadOnlyCollection<PatientProfile> PatientProfiles => _patientProfiles.AsReadOnly();

    private readonly List<DentistProfile> _dentistProfiles = new();
    public IReadOnlyCollection<DentistProfile> DentistProfiles => _dentistProfiles.AsReadOnly();

    private User() { } // EF Core

    public static User Create(string phoneNumber, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));

        var normalized = NormalizePhoneNumber(phoneNumber);
        var user = new User
        {
            PhoneNumber = phoneNumber.Trim(),
            PhoneNumberNormalized = normalized,
            IsActive = true,
            IsPhoneVerified = false,
            CreatedAtUtc = clock.UtcNow
        };
        return user;
    }

    public void AssignRole(Role role, IClock clock)
    {
        if (_roles.Any(r => r.RoleValue == role.Value)) return; // idempotent
        _roles.Add(UserRole.Create(Id, role, clock));
        Touch(clock.UtcNow);
    }

    public void RevokeRole(Role role, IClock clock)
    {
        var existing = _roles.FirstOrDefault(r => r.RoleValue == role.Value);
        if (existing is null) return;
        _roles.Remove(existing);
        Touch(clock.UtcNow);
    }

    public bool HasRole(Role role) => _roles.Any(r => r.RoleValue == role.Value);

    public void VerifyPhone(IClock clock)
    {
        IsPhoneVerified = true;
        PhoneVerifiedAtUtc = clock.UtcNow;
        Touch(clock.UtcNow);
    }

    public void SetPassword(string passwordHash, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        PasswordHash = passwordHash;
        Touch(clock.UtcNow);
    }

    public void RecordLogin(IClock clock)
    {
        LastLoginAtUtc = clock.UtcNow;
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

    /// <summary>
    /// Normalizes a phone number to E.164-ish form for the UNIQUE index.
    /// - Strip whitespace and dashes
    /// - Ensure leading "+"
    /// - Lowercase (defensive, no-op for digits)
    /// </summary>
    public static string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return string.Empty;
        var stripped = new string(phoneNumber.Where(c => char.IsDigit(c) || c == '+').ToArray());
        if (!stripped.StartsWith('+'))
        {
            // Iranian local number normalization: 09xxxxxxxxx → +989xxxxxxxxx
            if (stripped.StartsWith("0098"))
                stripped = "+" + stripped[2..];
            else if (stripped.StartsWith("098"))
                stripped = "+98" + stripped[3..];
            else if (stripped.StartsWith('0'))
                stripped = "+98" + stripped[1..];
            else
                stripped = "+" + stripped;
        }
        return stripped.ToLowerInvariant();
    }
}
