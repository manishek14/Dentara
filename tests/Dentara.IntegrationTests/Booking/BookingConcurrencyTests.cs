namespace Dentara.IntegrationTests.Booking;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Exceptions;
using Dentara.Domain.Services.Booking;
using Dentara.IntegrationTests.Fixtures;
using Dentara.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// CRITICAL concurrency test per ADR-003 acceptance criteria:
///   "در تست رقابتی با ۱۰۰ درخواست برای ۱۰ ظرفیت، تعداد رزروهای فعالِ موفق
///    از ظرفیت واقعی بیشتر نمی‌شود."
///
/// Translation: with 100 concurrent requests competing for 10 slots, the
/// number of successful active reservations must not exceed the real capacity.
/// </summary>
public sealed class BookingConcurrencyTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public BookingConcurrencyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static DateTimeOffset Utc(int hour) =>
        new DateTimeOffset(2026, 11, 20, hour, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ConcurrentBookings_100RequestsFor10Dentists_AtMost10Succeed()
    {
        // Arrange: 10 dentists, each with 1 dedicated dental unit
        var dentists = await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var clock = sp.GetRequiredService<IClock>();

            var dentistIds = new List<(Guid DentistId, Guid ResourceId, Guid ServiceId)>();
            for (int i = 0; i < 10; i++)
            {
                var user = Domain.Identity.User.Create($"+98912000{i:00000}", clock);
                user.VerifyPhone(clock);
                user.AssignRole(Domain.Identity.Role.Dentist, clock);
                db.Users.Add(user);

                var dentist = Domain.Identity.DentistProfile.Create(
                    user.Id, $"Dr. {i}", clock);
                db.DentistProfiles.Add(dentist);

                var service = Domain.Catalog.Service.Create(
                    $"Service {i}", 60, 500_000m, "IRR", clock);
                db.Services.Add(service);

                var ds = Domain.Catalog.DentistService.Create(
                    dentist.Id, service.Id, clock);
                db.DentistServices.Add(ds);

                var resource = Domain.Catalog.ClinicResource.Create(
                    $"Unit {i}", Domain.Catalog.ResourceType.DentalUnit, 1, clock,
                    ownerDentistId: dentist.Id);
                db.ClinicResources.Add(resource);

                var requirement = Domain.Catalog.ResourceServiceRequirement.Create(
                    service.Id, Domain.Catalog.ResourceType.DentalUnit, 1, clock,
                    requireDentistOwned: true);
                db.ResourceServiceRequirements.Add(requirement);

                dentistIds.Add((dentist.Id, resource.Id, service.Id));
            }
            await db.SaveChangesAsync();
            return dentistIds;
        });

        // 100 patients
        var patients = await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var clock = sp.GetRequiredService<IClock>();
            var patientIds = new List<Guid>();
            for (int i = 0; i < 100; i++)
            {
                var user = Domain.Identity.User.Create($"+98912900{i:00000}", clock);
                user.VerifyPhone(clock);
                user.AssignRole(Domain.Identity.Role.Patient, clock);
                db.Users.Add(user);
                patientIds.Add(user.Id);
            }
            await db.SaveChangesAsync();
            return patientIds;
        });

        // Each patient tries to book a RANDOM dentist at the SAME time [10:00, 11:00)
        var random = new Random(42);

        // Act: launch all 100 requests in parallel
        var successfulAppointments = new List<Guid>();
        var failureCount = 0;
        var failureTypes = new Dictionary<string, int>();

        var tasks = patients.Select(async p =>
        {
            try
            {
                var dentistChoice = dentists[random.Next(dentists.Count)];
                Appointment appointment;
                using (var scope = RootServiceProvider.CreateScope())
                {
                    var svc = scope.ServiceProvider.GetRequiredService<IBookingTransactionService>();
                    var request = new BookingRequest(
                        PatientId: p,
                        DentistId: dentistChoice.DentistId,
                        ServiceId: dentistChoice.ServiceId,
                        PublishedSessionId: null,
                        StartAtUtc: Utc(10),
                        EndAtUtc: Utc(11),
                        ServiceNameSnapshot: "Test Service",
                        ServiceDurationMinutesSnapshot: 60,
                        PriceAmountSnapshot: 500_000m,
                        CurrencySnapshot: "IRR",
                        DentistDisplayNameSnapshot: "Dr. Test",
                        CancellationPolicyVersion: 1,
                        CancellationFeePercent: 15m,
                        RefundablePercent: 85m,
                        TermsSnapshotHash: null,
                        RequiredResources: new List<ResourceRequirementItem>
                        {
                            new(1, 1, true, null)
                        });
                    appointment = await svc.ReserveAppointmentAsync(request);
                }
                lock (successfulAppointments)
                {
                    successfulAppointments.Add(appointment.Id);
                }
                return (Exception?)null;
            }
            catch (Exception ex)
            {
                lock (failureTypes)
                {
                    var typeName = ex.GetType().Name;
                    failureTypes[typeName] = failureTypes.GetValueOrDefault(typeName) + 1;
                }
                Interlocked.Increment(ref failureCount);
                return ex;
            }
        }).ToList();

        var exceptions = await Task.WhenAll(tasks);

        // Assert
        // ADR-003 acceptance criteria: successful active reservations must not
        // exceed the real capacity (10 dentists × 1 unit each = 10 slots).
        successfulAppointments.Count.Should().BeLessThanOrEqualTo(10,
            "because only 10 dentists × 1 unit each = 10 slots are available");

        // All failed requests should be domain exceptions (SlotUnavailable or
        // ResourceUnavailable), NOT infrastructure errors.
        var failedExceptions = exceptions.Where(e => e != null).ToList();
        failedExceptions.Should().NotBeEmpty("because 100 requests for 10 slots must have 90+ failures");

        foreach (var ex in failedExceptions)
        {
            ex!.Should().BeAssignableTo<DomainException>(
                $"all failures should be domain exceptions (SlotUnavailable or ResourceUnavailable), got {ex.GetType().Name}: {ex.Message}");
        }

        // Verify no two successful appointments overlap on the same dentist
        await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var appts = await db.Appointments
                .Where(a => successfulAppointments.Contains(a.Id))
                .ToListAsync();

            var byDentist = appts.GroupBy(a => a.DentistId);
            foreach (var group in byDentist)
            {
                group.Count().Should().Be(1,
                    $"dentist {group.Key} should have at most 1 appointment, but has {group.Count()}");
            }

            // Verify no two active resource allocations overlap
            var allocations = await db.ResourceAllocations
                .Where(ra => successfulAppointments.Contains(ra.AppointmentId))
                .ToListAsync();
            var byResource = allocations.GroupBy(a => a.ResourceId);
            foreach (var group in byResource)
            {
                group.Count().Should().Be(1,
                    $"resource {group.Key} should have at most 1 active allocation");
            }
        });

        _output.WriteLine(
            $"✅ Concurrency test PASSED: {successfulAppointments.Count} succeeded (≤10 capacity), " +
            $"{failureCount} failed (all domain exceptions).");
        _output.WriteLine("Failure types:");
        foreach (var kv in failureTypes)
            _output.WriteLine($"  {kv.Key}: {kv.Value}");
    }
}

