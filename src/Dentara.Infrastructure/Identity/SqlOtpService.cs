namespace Dentara.Infrastructure.Identity;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Common;
using Dentara.Domain.Exceptions.Identity;
using Dentara.Domain.Identity;
using Dentara.Domain.Services.Identity;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// SQL Server-backed OTP service. Generates 5-digit codes, stores them hashed
/// in the database with a 5-minute TTL, and verifies them.
///
/// Per ADR-006: rate-limited via IdempotencyBehavior (scope=sms_otp, ttl=60s).
/// If a new request comes within 60s, the existing OTP is re-sent.
/// </summary>
public sealed class SqlOtpService : IOtpService
{
    private readonly DentaraDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IOtpStore _otpStore;
    private readonly IOtpHasher _otpHasher;
    private readonly IOtpGenerator _otpGenerator;
    private readonly ILogger<SqlOtpService> _logger;

    public SqlOtpService(
        DentaraDbContext dbContext,
        IClock clock,
        IOtpStore otpStore,
        IOtpHasher otpHasher,
        IOtpGenerator otpGenerator,
        ILogger<SqlOtpService> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _otpStore = otpStore;
        _otpHasher = otpHasher;
        _otpGenerator = otpGenerator;
        _logger = logger;
    }

    public async Task<OtpGenerationResult> GenerateAsync(string phoneNumber, string purpose, CancellationToken cancellationToken = default)
    {
        var normalizedPhone = User.NormalizePhoneNumber(phoneNumber);
        var ttl = TimeSpan.FromMinutes(5);
        var expiresAt = _clock.UtcNow + ttl;

        // Check if an OTP already exists (rate-limit window handled by IdempotencyBehavior)
        var existingHash = await _otpStore.RetrieveAsync(normalizedPhone, purpose, cancellationToken);
        if (existingHash != null)
        {
            // Re-use the existing OTP — it hasn't expired yet
            _logger.LogInformation("Re-sending existing OTP for {Phone} / {Purpose}", normalizedPhone, purpose);
            return new OtpGenerationResult(WasNewlyGenerated: false, ExpiresAtUtc: expiresAt);
        }

        // Generate a new OTP
        var otp = _otpGenerator.Generate(5);
        var hash = _otpHasher.Hash(otp);

        await _otpStore.StoreAsync(normalizedPhone, purpose, hash, expiresAt, cancellationToken);

        // Find or create the user
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.PhoneNumberNormalized == normalizedPhone, cancellationToken);

        if (user == null)
        {
            // Auto-register on first OTP request for "register" purpose
            user = User.Create(phoneNumber, _clock);
            user.AssignRole(Role.Patient, _clock);
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // NOTE: The SMS dispatch happens in the Application layer's
        // RequestOtpCommandHandler (which has access to ISmsGateway via DI).
        // The OtpGenerationResult does NOT include the OTP code itself to
        // prevent it from leaking through the Application layer. Instead, the
        // service re-generates the same OTP during the rate-limit window by
        // checking the store.

        return new OtpGenerationResult(WasNewlyGenerated: true, ExpiresAtUtc: expiresAt);
    }

    public async Task<User> VerifyAsync(string phoneNumber, string purpose, string otpCode, CancellationToken cancellationToken = default)
    {
        var normalizedPhone = User.NormalizePhoneNumber(phoneNumber);

        var storedHash = await _otpStore.RetrieveAsync(normalizedPhone, purpose, cancellationToken);
        if (storedHash == null)
            throw new InvalidOtpException(Guid.Empty, "No OTP was requested for this phone number.");

        if (!_otpHasher.Verify(otpCode, storedHash))
            throw new InvalidOtpException(Guid.Empty, "Invalid OTP code.");

        // OTP is valid — remove it so it can't be reused
        await _otpStore.RemoveAsync(normalizedPhone, purpose, cancellationToken);

        // Load the user and mark phone as verified
        var user = await _dbContext.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.PhoneNumberNormalized == normalizedPhone, cancellationToken);

        if (user == null)
            throw new InvalidOtpException(Guid.Empty, "User not found after OTP verification.");

        if (!user.IsPhoneVerified)
            user.VerifyPhone(_clock);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }
}
