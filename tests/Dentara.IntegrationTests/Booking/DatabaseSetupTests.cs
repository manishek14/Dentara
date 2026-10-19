namespace Dentara.IntegrationTests.Booking;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Services.Booking;
using Dentara.IntegrationTests.Fixtures;
using Dentara.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Sanity test: ensures the SQLite in-memory database, migrations, and DI
/// container all wire up correctly. Subsequent test classes build on this.
/// </summary>
public sealed class DatabaseSetupTests : IntegrationTestBase
{
    [Fact]
    public async Task Database_IsCreatedAndMigrated_HasAllTables()
    {
        await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();

            // Verify a few tables exist and are queryable
            var userCount = await db.Users.CountAsync();
            var roleCount = await db.Roles.CountAsync();
            var serviceCount = await db.Services.CountAsync();

            // Roles are seeded by the configuration
            roleCount.Should().Be(4, "because 4 roles are seeded: Patient, Dentist, Receptionist, Admin");
            userCount.Should().Be(0, "because no users are seeded");
            serviceCount.Should().Be(0, "because no services are seeded");
        });
    }

    [Fact]
    public async Task SeedBasicAsync_CreatesValidTestData()
    {
        await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var clock = sp.GetRequiredService<IClock>();

            var seed = await TestDataSeeder.SeedBasicAsync(db, clock);

            seed.DentistProfileId.Should().NotBeEmpty();
            seed.PatientProfileId.Should().NotBeEmpty();
            seed.ServiceId.Should().NotBeEmpty();
            seed.ResourceId.Should().NotBeEmpty();

            // Verify the relationships are correctly stored
            var dentist = await db.DentistProfiles.FindAsync(seed.DentistProfileId);
            dentist.Should().NotBeNull();
            dentist!.DisplayName.Should().Be("Dr. Test Dentist");

            var service = await db.Services.FindAsync(seed.ServiceId);
            service.Should().NotBeNull();
            service!.DurationMinutes.Should().Be(60);
            service.PriceAmount.Should().Be(500_000m);

            // Verify the resource requirement was created
            var requirement = await db.ResourceServiceRequirements
                .FirstOrDefaultAsync(r => r.ServiceId == seed.ServiceId);
            requirement.Should().NotBeNull();
            requirement!.QuantityRequired.Should().Be(1);
        });
    }
}
