namespace Dentara.Infrastructure.Identity;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Common;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// In-memory OTP store. Production would use a database table with TTL
/// or Redis. For development and testing, this is sufficient.
/// </summary>
public sealed class InMemoryOtpStore : IOtpStore
{
    private readonly Dictionary<string, (string Hash, DateTimeOffset ExpiresAtUtc)> _storage = new();
    private readonly IClock _clock;
    private readonly ILogger<InMemoryOtpStore> _logger;

    public InMemoryOtpStore(IClock clock, ILogger<InMemoryOtpStore> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    public Task StoreAsync(string phoneNumber, string purpose, string otpHash, DateTimeOffset expiresAtUtc, CancellationToken ct = default)
    {
        var key = $"{phoneNumber}|{purpose}";
        _storage[key] = (otpHash, expiresAtUtc);
        _logger.LogDebug("Stored OTP for {Key}, expires at {ExpiresAt}", key, expiresAtUtc);
        return Task.CompletedTask;
    }

    public Task<string?> RetrieveAsync(string phoneNumber, string purpose, CancellationToken ct = default)
    {
        var key = $"{phoneNumber}|{purpose}";
        if (!_storage.TryGetValue(key, out var entry))
            return Task.FromResult<string?>(null);

        if (entry.ExpiresAtUtc <= _clock.UtcNow)
        {
            _storage.Remove(key);
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(entry.Hash);
    }

    public Task RemoveAsync(string phoneNumber, string purpose, CancellationToken ct = default)
    {
        var key = $"{phoneNumber}|{purpose}";
        _storage.Remove(key);
        return Task.CompletedTask;
    }
}

/// <summary>
/// SHA-256 OTP hasher. Production would use BCrypt or PBKDF2 for stronger
/// hashing, but SHA-256 is sufficient for short-lived 5-digit OTPs.
/// </summary>
public sealed class Sha256OtpHasher : IOtpHasher
{
    public string Hash(string otp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
        return Convert.ToHexString(bytes);
    }

    public bool Verify(string otp, string hash)
    {
        return string.Equals(Hash(otp), hash, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Random OTP generator using System.Security.Cryptography.RandomNumberGenerator.
/// </summary>
public sealed class CryptoOtpGenerator : IOtpGenerator
{
    public string Generate(int digits = 5)
    {
        var bytes = RandomNumberGenerator.GetBytes(digits);
        var sb = new StringBuilder();
        foreach (var b in bytes)
            sb.Append((b % 10).ToString());
        return sb.ToString();
    }
}

/// <summary>
/// No-op current user context — production would read from JWT claims via
/// IHttpContextAccessor. Returns anonymous user; the API layer will replace
/// this with a real implementation.
/// </summary>
public sealed class NoOpCurrentUserContext : ICurrentUserContext
{
    public Guid? UserId => null;
    public bool IsAuthenticated => false;
    public IReadOnlyCollection<string> Roles => Array.Empty<string>();

    public bool IsInRole(string role) => false;

    public void EnsureAuthenticated()
        => throw new InvalidOperationException("Not authenticated (NoOpCurrentUserContext).");

    public void EnsureInRole(string role)
        => throw new InvalidOperationException($"Not in role '{role}' (NoOpCurrentUserContext).");
}
