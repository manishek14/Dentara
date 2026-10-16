namespace Dentara.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration for the Infrastructure layer's persistence concerns.
/// </summary>
public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DentaraDb")
            ?? throw new InvalidOperationException("Connection string 'DentaraDb' not found.");

        services.AddDbContext<DentaraDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(DentaraDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });

            // Performance: avoid tracking for read-heavy scenarios in queries
            // (handlers can opt-in via AsNoTracking explicitly).
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll);
        });

        services.AddScoped<IDbContextAccessor>(sp => new DbContextAccessor(sp.GetRequiredService<DentaraDbContext>()));
        return services;
    }
}

/// <summary>
/// Lightweight wrapper so Application layer code does not depend on DbContext directly.
/// </summary>
public interface IDbContextAccessor
{
    DentaraDbContext Context { get; }
}

internal sealed class DbContextAccessor : IDbContextAccessor
{
    public DentaraDbContext Context { get; }

    public DbContextAccessor(DentaraDbContext context) => Context = context;
}
