namespace Dentara.IntegrationTests.Api;

using Dentara.Domain.Common;
using Dentara.Infrastructure.Persistence;
using Dentara.Infrastructure.Clock;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// WebApplicationFactory that replaces SQL Server with SQLite in-memory for
/// fast, isolated E2E tests. The SQLite connection is kept open for the
/// lifetime of the factory so the in-memory database persists across requests.
///
/// Also replaces IClock with TestClock so tests can control time.
/// </summary>
public sealed class DentaraWebAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;
    public TestClock Clock { get; } = new();

    public DentaraWebAppFactory()
    {
        // Set JWT secrets as environment variables BEFORE the web host starts.
        // Program.cs reads these via IConfiguration during AddApi().
        Environment.SetEnvironmentVariable("Jwt__AccessTokenSecret",
            "DockerDev_Secret_Key_At_Least_32_Characters_Long_2026");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "Dentara");
        Environment.SetEnvironmentVariable("Jwt__Audience", "Dentara");

        _connection = new SqliteConnection("DataSource=:memory:;Cache=Shared;Pooling=True");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override JWT settings for tests — must match JwtTestHelper
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:AccessTokenSecret"] = "DockerDev_Secret_Key_At_Least_32_Characters_Long_2026",
                ["Jwt:Issuer"] = "Dentara",
                ["Jwt:Audience"] = "Dentara",
                ["ConnectionStrings:DentaraDb"] = "DataSource=:memory:;Cache=Shared;Pooling=True"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove ALL existing DbContext-related registrations (SqlServer)
            var descriptors = services
                .Where(d => d.ServiceType.Namespace?.Contains("EntityFrameworkCore") == true
                    || d.ServiceType == typeof(DentaraDbContext)
                    || d.ServiceType == typeof(DbContextOptions<DentaraDbContext>)
                    || d.ServiceType == typeof(DbContextOptions))
                .ToList();
            foreach (var d in descriptors)
                services.Remove(d);

            // Add SQLite DbContext using the shared connection
            services.AddDbContext<DentaraDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Re-register IDbContextAccessor (internal) — needed by
            // SqlIdempotencyService and other Infrastructure services.
            services.AddScoped<Dentara.Infrastructure.Persistence.IDbContextAccessor>(
                sp => new Dentara.Infrastructure.Persistence.DbContextAccessor(
                    sp.GetRequiredService<DentaraDbContext>()));

            // MediatR handlers from Infrastructure are auto-discovered via
            // RegisterServicesFromAssemblies in AddApplication (both Application
            // and Infrastructure assemblies are scanned).

            // Replace IClock with TestClock
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(Clock);

            // Replace ICurrentUserContext with HttpCurrentUserContext (reads JWT claims)
            services.RemoveAll<Dentara.Application.Common.Interfaces.ICurrentUserContext>();
            services.AddScoped<Dentara.Application.Common.Interfaces.ICurrentUserContext, Dentara.Api.Authentication.HttpCurrentUserContext>();

            // Ensure IHttpContextAccessor is registered
            services.AddHttpContextAccessor();

            // Remove ALL background workers — they cause DbContext conflicts in tests
            var workerDescriptors = services
                .Where(d => d.ImplementationType?.Namespace?.Contains("BackgroundJobs") == true
                    || d.ImplementationType?.Name?.EndsWith("Worker") == true)
                .ToList();
            foreach (var d in workerDescriptors)
                services.Remove(d);

            // Build the service provider and create the schema
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DentaraDbContext>();
            db.Database.EnsureCreated();

            // Seed reference data
            SeedData(db, Clock);
        });
    }

    private static void SeedData(DentaraDbContext db, IClock clock)
    {
        // Roles are seeded by EF Core configuration (HasData)
        db.SaveChanges();

        // Create a test dentist + patient + service + resource
        var dentistUser = Domain.Identity.User.Create("+989120000001", clock);
        dentistUser.VerifyPhone(clock);
        dentistUser.AssignRole(Domain.Identity.Role.Dentist, clock);
        db.Users.Add(dentistUser);

        var patientUser = Domain.Identity.User.Create("+989120000002", clock);
        patientUser.VerifyPhone(clock);
        patientUser.AssignRole(Domain.Identity.Role.Patient, clock);
        db.Users.Add(patientUser);

        var adminUser = Domain.Identity.User.Create("+989120000003", clock);
        adminUser.VerifyPhone(clock);
        adminUser.AssignRole(Domain.Identity.Role.Admin, clock);
        db.Users.Add(adminUser);

        var dentistProfile = Domain.Identity.DentistProfile.Create(
            dentistUser.Id, "Dr. Test", clock, "General Dentist", "LIC-001");
        db.DentistProfiles.Add(dentistProfile);

        var patientProfile = Domain.Identity.PatientProfile.Create(
            patientUser.Id, "Test", "Patient", clock);
        db.PatientProfiles.Add(patientProfile);

        var service = Domain.Catalog.Service.Create(
            "Cavity Filling", 60, 500_000m, "IRR", clock);
        db.Services.Add(service);

        var ds = Domain.Catalog.DentistService.Create(dentistProfile.Id, service.Id, clock);
        db.DentistServices.Add(ds);

        var resource = Domain.Catalog.ClinicResource.Create(
            "Dental Unit #1", Domain.Catalog.ResourceType.DentalUnit, 1, clock);
        db.ClinicResources.Add(resource);

        var req = Domain.Catalog.ResourceServiceRequirement.Create(
            service.Id, Domain.Catalog.ResourceType.DentalUnit, 1, clock);
        db.ResourceServiceRequirements.Add(req);

        db.SaveChanges();

        DentistUserId = dentistUser.Id;
        PatientUserId = patientUser.Id;
        AdminUserId = adminUser.Id;
        DentistProfileId = dentistProfile.Id;
        ServiceId = service.Id;
        ResourceId = resource.Id;
    }

    // Seeded IDs accessible from tests
    public static Guid DentistUserId { get; private set; }
    public static Guid PatientUserId { get; private set; }
    public static Guid AdminUserId { get; private set; }
    public static Guid DentistProfileId { get; private set; }
    public static Guid ServiceId { get; private set; }
    public static Guid ResourceId { get; private set; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Test clock with controllable time.
/// </summary>
public sealed class TestClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } =
        new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    public void AdvanceBy(TimeSpan duration) => UtcNow = UtcNow + duration;

    public void SetTo(int year, int month, int day, int hour = 0, int minute = 0) =>
        UtcNow = new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);
}
