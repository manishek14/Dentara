namespace Dentara.Application.Identity.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Common;
using Dentara.Domain.Identity;
using MediatR;

/// <summary>
/// Requests an OTP for a phone number. If the user doesn't exist, creates them
/// (registration flow). If they exist, sends OTP for login. The OTP dispatch
/// is rate-limited via IdempotencyRecord (60s per phone+purpose).
/// </summary>
public sealed record RequestOtpCommand(
    string PhoneNumber,
    string Purpose, // "login", "register", "reset_password"
    string? CorrelationId = null)
    : ICommand<OtpRequestResult>, IHasCorrelationId, IIdempotentRequest
{
    public string Scope => "sms_otp";
    public string Key => $"{PhoneNumber}|{Purpose}";
    public TimeSpan? Ttl => TimeSpan.FromMinutes(1); // 60s rate limit per ADR-006
}

public sealed record OtpRequestResult(
    bool WasNewlyGenerated,
    DateTimeOffset ExpiresAtUtc);
