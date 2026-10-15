namespace Dentara.Domain.Services.Identity;

using Dentara.Domain.Identity;

/// <summary>
/// Domain service for OTP-based authentication. Encapsulates OTP generation,
/// rate limiting (via IdempotencyRecord), and verification. The actual SMS
/// dispatch is a side effect dispatched via OtpRequestedEvent.
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generates and dispatches an OTP for the given phone number and purpose.
    /// Rate-limited: if an OTP was generated in the last 60 seconds for the
    /// same phone+purpose, the existing OTP is re-sent (per ADR-006).
    /// </summary>
    Task<OtpGenerationResult> GenerateAsync(
        string phoneNumber,
        string purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies an OTP. If valid, marks the user's phone as verified (if not
    /// already) and returns the user. If invalid/expired, throws InvalidOtpException.
    /// </summary>
    Task<User> VerifyAsync(
        string phoneNumber,
        string purpose,
        string otpCode,
        CancellationToken cancellationToken = default);
}

public sealed record OtpGenerationResult(
    bool WasNewlyGenerated, // false if rate-limited re-send of existing OTP
    DateTimeOffset ExpiresAtUtc);
