namespace Dentara.Infrastructure;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Common;
using Dentara.Domain.Identity;
using Dentara.Domain.Booking;
using Dentara.Domain.Payments;
using Dentara.Domain.Scheduling;
using Dentara.Domain.Catalog;
using Dentara.Infrastructure.Clock;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Top-level DI registration for the Infrastructure layer.
/// Api layer calls AddInfrastructure(configuration) from Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Persistence (DbContext + repositories + UoW + dispatcher)
        services.AddPersistence(configuration);

        // Clock
        services.AddSingleton<IClock, Clock.SystemClock>();

        // Unit of Work — scoped per request
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Generic repositories for each aggregate
        services.AddScoped(typeof(IRepository<>), typeof(EfCoreRepository<>));

        // Domain event dispatcher
        services.AddScoped<IDomainEventDispatcher, MediatrDomainEventDispatcher>();

        // Idempotency service
        services.AddScoped<IIdempotencyService, SqlIdempotencyService>();

        return services;
    }
}
