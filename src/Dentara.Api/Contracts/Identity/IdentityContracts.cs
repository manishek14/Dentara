namespace Dentara.Api.Contracts.Identity;

/// <summary>
/// Request body for POST /api/identity/otp/request.
/// </summary>
public sealed record RequestOtpRequest(
    string PhoneNumber,
    string Purpose); // "login" | "register" | "reset_password"

/// <summary>
/// Response body for POST /api/identity/otp/request.
/// </summary>
public sealed record RequestOtpResponse(
    bool WasNewlyGenerated,
    DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Request body for POST /api/identity/otp/verify.
/// </summary>
public sealed record VerifyOtpRequest(
    string PhoneNumber,
    string Purpose,
    string OtpCode);

/// <summary>
/// Response body for POST /api/identity/otp/verify.
/// </summary>
public sealed record VerifyOtpResponse(
    Guid UserId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyCollection<string> Roles);
