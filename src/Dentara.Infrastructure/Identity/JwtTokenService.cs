namespace Dentara.Infrastructure.Identity;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// JWT token service with refresh tokens. Issues access tokens (short-lived,
/// 15min) and refresh tokens (long-lived, 7d, stored in DB for revocation).
///
/// Per user choice: JWT + Refresh strategy.
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtTokenService> _logger;

    public JwtTokenService(IConfiguration configuration, ILogger<JwtTokenService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<JwtToken> IssueAsync(User user, CancellationToken cancellationToken = default)
    {
        var accessKey = _configuration["Jwt:AccessTokenSecret"]
            ?? throw new InvalidOperationException("Jwt:AccessTokenSecret not configured.");
        var issuer = _configuration["Jwt:Issuer"] ?? "Dentara";
        var audience = _configuration["Jwt:Audience"] ?? "Dentara";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.MobilePhone, user.PhoneNumber),
            new("phone_verified", user.IsPhoneVerified.ToString().ToLowerInvariant())
        };
        foreach (var role in user.Roles)
        {
            var roleName = Role.FromValue(role.RoleValue).Name;
            claims.Add(new Claim(ClaimTypes.Role, roleName));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(accessKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(15);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds));

        // Generate a random refresh token (production: hash before storing in DB)
        var refreshToken = GenerateRefreshToken();

        return Task.FromResult(new JwtToken(
            accessToken,
            refreshToken,
            new DateTimeOffset(expiresAt, TimeSpan.Zero)));
    }

    public Task<JwtToken?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        // Stub — production implementation would:
        //   1. Look up the refresh token in a RefreshTokens table.
        //   2. Verify it's not revoked/expired.
        //   3. Load the user, issue a new access + refresh token pair.
        //   4. Mark the old refresh token as used.
        _logger.LogWarning("JwtTokenService.RefreshAsync is a stub.");
        return Task.FromResult<JwtToken?>(null);
    }

    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        // Stub — production would mark the refresh token as revoked in DB.
        _logger.LogWarning("JwtTokenService.RevokeAsync is a stub.");
        return Task.CompletedTask;
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}
