namespace Dentara.Application.Common.Interfaces;

using Dentara.Domain.Common;
using Dentara.Domain.Payments;

/// <summary>
/// Abstraction over the current authenticated user context. The API layer
/// populates this from JWT claims; the Application layer reads it for
/// authorization checks and audit logging.
/// </summary>
public interface ICurrentUserContext
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Roles { get; }

    bool IsInRole(string role);
    void EnsureAuthenticated(); // throws if not authenticated
    void EnsureInRole(string role); // throws if not in role
}

/// <summary>
/// Abstraction over OTP storage. Production uses the database (with TTL);
/// tests use an in-memory implementation.
/// </summary>
public interface IOtpStore
{
    Task StoreAsync(string phoneNumber, string purpose, string otpHash, DateTimeOffset expiresAtUtc, CancellationToken ct = default);
    Task<string?> RetrieveAsync(string phoneNumber, string purpose, CancellationToken ct = default);
    Task RemoveAsync(string phoneNumber, string purpose, CancellationToken ct = default);
}

/// <summary>
/// OTP hash function abstraction. Production uses BCrypt or PBKDF2;
/// tests can use a plain identity function.
/// </summary>
public interface IOtpHasher
{
    string Hash(string otp);
    bool Verify(string otp, string hash);
}

/// <summary>
/// Abstraction over OTP generation.
/// </summary>
public interface IOtpGenerator
{
    string Generate(int digits = 5);
}
