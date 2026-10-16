namespace Dentara.Application.Identity.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Results;
using Dentara.Domain.Identity;
using MediatR;

/// <summary>
/// Verifies an OTP. On success, returns a JWT + refresh token. On failure,
/// returns Result.Failure("IDENTITY_INVALID_OTP", ...).
/// </summary>
public sealed record VerifyOtpCommand(
    string PhoneNumber,
    string Purpose,
    string OtpCode,
    string? CorrelationId = null)
    : ICommand<VerifyOtpResult>, IHasCorrelationId
{
}

public sealed record VerifyOtpResult(
    Guid UserId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyCollection<string> Roles);
