namespace Dentara.IntegrationTests.Fixtures;

using Dentara.Application;
using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Common;
using Dentara.Domain.Services.Booking;
using Dentara.Domain.Services.Identity;
using Dentara.Infrastructure;
using Dentara.Infrastructure.Booking;
using Dentara.Infrastructure.Clock;
using Dentara.Infrastructure.Gateways.Payments;
using Dentara.Infrastructure.Gateways.Sms;
using Dentara.Infrastructure.Identity;
using Dentara.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public static class TestServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Infrastructure services with a SQLite in-memory database
    /// instead of SQL Server. The connection is shared across the test's lifetime
    /// to keep the in-memory DB alive.
    /// </summary>
    public static IServiceCollection AddInfrastructureTest(
        this IServiceCollection services,
        SqliteConnection connection,
        TestClock? clock = null)
    {
        // DbContext with SQLite — connection kept open externally
        services.AddDbContext<DentaraDbContext>(options =>
        {
            options.UseSqlite(connection, sql =>
            {
                sql.MigrationsAssembly(typeof(DentaraDbContext).Assembly.FullName);
            });
        });

        // Register the DbContext also as IDbContextAccessor's underlying type
        services.AddScoped<IDbContextAccessor>(sp => new DbContextAccessor(
            sp.GetRequiredService<DentaraDbContext>()));

        // Clock — use TestClock if provided, else SystemClock
        if (clock != null)
        {
            services.AddSingleton<IClock>(clock);
        }
        else
        {
            services.AddSingleton<IClock, Infrastructure.Clock.SystemClock>();
        }

        // Unit of Work
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();

        // Generic repositories
        services.AddScoped(typeof(IRepository<>), typeof(EfCoreRepository<>));

        // Domain event dispatcher
        services.AddScoped<IDomainEventDispatcher, MediatrDomainEventDispatcher>();

        // Idempotency service
        services.AddScoped<IIdempotencyService, SqlIdempotencyService>();

        // Domain services
        services.AddScoped<IBookingTransactionService, SqlBookingTransactionService>();
        services.AddScoped<ICancellationService, SqlCancellationService>();

        // Gateway adapters — use sandbox for tests
        services.AddScoped<IPaymentGateway, SandboxPaymentGateway>();
        services.AddScoped<ISmsGateway, SandboxSmsGateway>();

        // Identity services
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IOtpService, SqlOtpService>();
        services.AddSingleton<IOtpStore, InMemoryOtpStore>();
        services.AddSingleton<IOtpHasher, Sha256OtpHasher>();
        services.AddSingleton<IOtpGenerator, CryptoOtpGenerator>();
        services.AddScoped<ICurrentUserContext, NoOpCurrentUserContext>();

        return services;
    }
}

