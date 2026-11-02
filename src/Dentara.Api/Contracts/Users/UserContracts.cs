namespace Dentara.Api.Contracts.Users;

// ─── User ───

public sealed record UserResponse(
    Guid Id,
    string PhoneNumber,
    bool IsActive,
    bool IsPhoneVerified,
    DateTimeOffset? LastLoginAtUtc,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<string> Roles,
    PatientProfileResponse? PatientProfile,
    DentistProfileResponse? DentistProfile);

public sealed record PatientProfileResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string? NationalCode,
    DateTimeOffset? DateOfBirth);

public sealed record DentistProfileResponse(
    Guid Id,
    string DisplayName,
    string? Specialization,
    string? LicenseNumber,
    bool IsActive);

public sealed record UpdateUserStatusRequest(
    bool IsActive);

public sealed record UpdatePatientProfileRequest(
    string FirstName,
    string LastName,
    string? NationalCode,
    DateTimeOffset? DateOfBirth,
    string? Notes);

public sealed record UpdateDentistProfileRequest(
    string DisplayName,
    string? Specialization,
    string? LicenseNumber,
    string? Bio);

// ─── Role Assignment ───

public sealed record AssignRoleRequest(
    Guid UserId,
    string RoleName); // "Patient", "Dentist", "Receptionist", "Admin"

public sealed record RevokeRoleRequest(
    Guid UserId,
    string RoleName);
