namespace Dentara.Api.Extensions;

using Dentara.Application.Common.Interfaces;
using Dentara.Api.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

/// <summary>
/// DI registration for API-layer concerns: authentication, current user context,
/// HTTP context accessor, and CORS.
/// </summary>
public static class ApiDependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        // IHttpContextAccessor — required for HttpCurrentUserContext
        services.AddHttpContextAccessor();

        // Replace the NoOp current user context from Infrastructure with the real one
        services.AddScoped<ICurrentUserContext, HttpCurrentUserContext>();

        // JWT authentication
        var jwtSecret = configuration["Jwt:AccessTokenSecret"]
            ?? throw new InvalidOperationException("Jwt:AccessTokenSecret not configured.");
        var issuer = configuration["Jwt:Issuer"] ?? "Dentara";
        var audience = configuration["Jwt:Audience"] ?? "Dentara";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
            });

        services.AddAuthorization();

        // CORS — allow any origin in Development; tighten for production
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        return services;
    }
}
