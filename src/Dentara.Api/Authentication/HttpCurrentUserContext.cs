namespace Dentara.Api.Authentication;

using Dentara.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

/// <summary>
/// HTTP-context-backed implementation of ICurrentUserContext. Reads the user's
/// ID and roles from JWT claims (set by JwtBearer authentication handler).
/// </summary>
public sealed class HttpCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var sub = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public IReadOnlyCollection<string> Roles =>
        (_httpContextAccessor.HttpContext?.User?
            .FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList() as IReadOnlyCollection<string>) ?? Array.Empty<string>();

    public bool IsInRole(string role) => Roles.Contains(role);

    public void EnsureAuthenticated()
    {
        if (!IsAuthenticated)
            throw new UnauthorizedAccessException("Authentication required.");
    }

    public void EnsureInRole(string role)
    {
        EnsureAuthenticated();
        if (!IsInRole(role))
            throw new UnauthorizedAccessException($"Role '{role}' required.");
    }
}
