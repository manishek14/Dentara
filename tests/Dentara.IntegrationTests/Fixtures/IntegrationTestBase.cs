namespace Dentara.IntegrationTests.Fixtures;

using Dentara.Application;
using Dentara.Domain.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for integration tests. Creates a fresh SQLite in-memory database
/// for each test class, applies the schema via EF Core migrations, and provides
/// a configured DI container with all Application + Infrastructure services.
///
/// SQLite is used instead of SQL Server because:
///   - No Docker dependency (Testcontainers requires Docker).
///   - Real relational behavior (FK constraints, CHECK constraints, UNIQUE
///     constraints, transactions) — unlike EF Core InMemory which is just a
///     dictionary wrapper.
///   - ACID transactions with SERIALIZABLE-like isolation via BEGIN IMMEDIATE.
///
/// NOTE: SQLite does NOT support UPDLOCK/HOLDLOCK hints. The booking transaction
/// service uses SERIALIZABLE isolation, which SQLite approximates via
/// BEGIN IMMEDIATE TRANSACTION. This is sufficient to test the overlap-check
/// logic; the actual SQL Server lock hints are exercised in production.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;
    private bool _disposed;

    /// <summary>
    /// The root service provider. Test classes use this to create scopes for
    /// parallel operations (e.g. concurrency tests that need multiple isolated
    /// scopes running in parallel).
    /// </summary>
    protected ServiceProvider RootServiceProvider => _serviceProvider;

    protected IntegrationTestBase()
    {
        // Shared in-memory connection — keeps the DB alive for the test's lifetime
        _connection = new SqliteConnection("DataSource=:memory:;Cache=Shared;Pooling=True");
        _connection.Open();

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug().SetMinimumLevel(LogLevel.Warning));
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Ensure the schema is created. Use EnsureCreated (not Migrate) because
        // the SQLite-compatible model in DentaraDbContext strips T-SQL CHECK
        // constraints that don't work in SQLite.
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Dentara.Infrastructure.Persistence.DentaraDbContext>();
        dbContext.Database.EnsureCreated();
    }

    /// <summary>
    /// Override to register additional test-specific services.
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
        // Default: register all production services with SQLite override
        services.AddApplication();
        services.AddInfrastructureTest(_connection);
    }

    /// <summary>
    /// Resolves a service from the DI container. Each resolution is scoped
    /// to a new scope so scoped services (DbContext, repositories) are fresh.
    /// </summary>
    protected T Resolve<T>() where T : notnull
    {
        var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Resolves a scoped service and runs an action against it inside a scope.
    /// </summary>
    protected async Task UsingScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = _serviceProvider.CreateScope();
        await action(scope.ServiceProvider);
    }

    /// <summary>
    /// Resolves a scoped service and runs a function against it inside a scope.
    /// </summary>
    protected async Task<T> UsingScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = _serviceProvider.CreateScope();
        return await action(scope.ServiceProvider);
    }

    /// <summary>
    /// A deterministic clock for tests. Set UtcNow to control time-based logic.
    /// </summary>
    protected TestClock Clock { get; } = new();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _serviceProvider.Dispose();
        _connection.Dispose();
        _loggerFactory.Dispose();
    }
}

/// <summary>
/// Controllable clock for tests. Defaults to a fixed UTC instant; tests can
/// advance time by setting UtcNow.
/// </summary>
public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } =
        new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    public void AdvanceBy(TimeSpan duration) => UtcNow = UtcNow + duration;

    public void SetTo(int year, int month, int day, int hour = 0, int minute = 0) =>
        UtcNow = new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
