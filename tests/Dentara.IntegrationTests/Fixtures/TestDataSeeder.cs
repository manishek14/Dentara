namespace Dentara.IntegrationTests.Fixtures;

using Dentara.Domain.Booking;
using Dentara.Domain.Catalog;
using Dentara.Domain.Common;
using Dentara.Domain.Identity;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Helpers for seeding reference data into the test database.
/// </summary>
public static class TestDataSeeder
{
    /// <summary>
    /// Seeds a dentist + patient + service + dental unit resource.
    /// Returns the IDs for use in tests.
    /// </summary>
    public static async Task<SeedData> SeedBasicAsync(DentaraDbContext db, IClock clock)
    {
        // User accounts
        var dentistUser = User.Create("+989120000001", clock);
        dentistUser.VerifyPhone(clock);
        dentistUser.AssignRole(Role.Dentist, clock);

        var patientUser = User.Create("+989120000002", clock);
        patientUser.VerifyPhone(clock);
        patientUser.AssignRole(Role.Patient, clock);

        db.Users.AddRange(dentistUser, patientUser);

        // Profiles
        var dentistProfile = DentistProfile.Create(
            dentistUser.Id, "Dr. Test Dentist", clock,
            specialization: "General Dentist", licenseNumber: "LIC-001");
        var patientProfile = PatientProfile.Create(
            patientUser.Id, "Test", "Patient", clock);

        db.DentistProfiles.Add(dentistProfile);
        db.PatientProfiles.Add(patientProfile);

        // Service
        var service = Service.Create(
            "Cavity Filling", 60, 500_000m, "IRR", clock);
        db.Services.Add(service);

        // DentistService link (no overrides)
        var dentistService = DentistService.Create(
            dentistProfile.Id, service.Id, clock);
        db.DentistServices.Add(dentistService);

        // Resource: a shared dental unit (capacity 1)
        var resource = ClinicResource.Create(
            "Dental Unit #1", ResourceType.DentalUnit, capacity: 1, clock);
        db.ClinicResources.Add(resource);

        // Resource requirement: service needs 1 DentalUnit
        var requirement = ResourceServiceRequirement.Create(
            service.Id, ResourceType.DentalUnit, quantityRequired: 1, clock);
        db.ResourceServiceRequirements.Add(requirement);

        await db.SaveChangesAsync();

        return new SeedData(
            dentistUser.Id,
            patientUser.Id,
            dentistProfile.Id,
            patientProfile.Id,
            service.Id,
            resource.Id);
    }
}

public sealed record SeedData(
    Guid DentistUserId,
    Guid PatientUserId,
    Guid DentistProfileId,
    Guid PatientProfileId,
    Guid ServiceId,
    Guid ResourceId);
