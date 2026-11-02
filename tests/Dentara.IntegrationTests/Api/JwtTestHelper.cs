namespace Dentara.IntegrationTests.Api;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Generates valid JWT tokens for E2E tests. Uses the same secret as
/// configured in DentaraWebAppFactory (overridden via environment).
/// </summary>
internal static class JwtTestHelper
{
    private const string Secret = "DockerDev_Secret_Key_At_Least_32_Characters_Long_2026";
    private const string Issuer = "Dentara";
    private const string Audience = "Dentara";

    public static string GenerateToken(Guid userId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.NameId, userId.ToString()),
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("phone_verified", "true")
        };
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
