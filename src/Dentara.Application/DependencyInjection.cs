namespace Dentara.Application;

using Dentara.Application.Common.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

/// <summary>
/// DI registration for the Application layer. The Infrastructure and API layers
/// call this from their Configure() methods.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
        });

        return services;
    }

    /// <summary>
    /// Registers MediatR handlers from an additional assembly (e.g. Infrastructure).
    /// Call this AFTER AddApplication from the Infrastructure layer.
    /// </summary>
    public static IServiceCollection AddMediatRFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        return services;
    }
}
