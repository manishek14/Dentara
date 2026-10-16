namespace Dentara.Application.Common.Interfaces;

using Dentara.Domain.Identity;

/// <summary>
/// JWT token service abstraction. Issues access + refresh token pairs.
/// Implementation lives in Infrastructure (uses System.IdentityModel.Tokens.Jwt
/// or a third-party JWT library).
/// </summary>
public interface IJwtTokenService
{
    Task<JwtToken> IssueAsync(User user, CancellationToken cancellationToken = default);
    Task<JwtToken?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default);
}

public sealed record JwtToken(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc);
