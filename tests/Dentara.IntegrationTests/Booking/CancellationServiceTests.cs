namespace Dentara.IntegrationTests.Booking;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Payments;
using Dentara.Domain.Services.Booking;
using Dentara.IntegrationTests.Fixtures;
using Dentara.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

/// <summary>
/// Tests for SqlCancellationService — verifies atomic cancellation, capacity
/// release, and refund row creation per ADR-003.
///
/// NOTE: Most tests in this class are currently SKIPPED due to an
/// incompatibility between EF Core 10's IsRowVersion() configuration and
/// SQLite's lack of rowversion support. The booking service tests (which
/// only INSERT, not UPDATE) work fine. Cancellation requires UPDATE which
/// triggers EF Core's optimistic concurrency check on RowVersion, which
/// fails on SQLite because RowVersion never changes (no auto-generation).
///
/// These tests will be re-enabled when CI adds a SQL Server Testcontainers
/// fixture (production target database). The booking concurrency test
/// (BookingConcurrencyTests) already validates the anti-overbooking guarantee.
/// </summary>
public sealed class CancellationServiceTests : IntegrationTestBase
{
    private static DateTimeOffset Utc(int hour) =>
        new DateTimeOffset(2026, 11, 25, hour, 0, 0, TimeSpan.Zero);

    private static BookingRequest BuildBookingRequest(
        SeedData seed, DateTimeOffset start, DateTimeOffset end) =>
        new(
            PatientId: seed.PatientUserId,
            DentistId: seed.DentistProfileId,
            ServiceId: seed.ServiceId,
            PublishedSessionId: null,
            StartAtUtc: start,
            EndAtUtc: end,
            ServiceNameSnapshot: "Cavity Filling",
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
                new(1, 1, false, null)
            });

    [Fact]
    public async Task Cancel_FromPendingPayment_ReleasesAllocationsWithoutRefund()
    {
        // Arrange
        var seed = await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var clock = sp.GetRequiredService<IClock>();
            return await TestDataSeeder.SeedBasicAsync(db, clock);
        });

        var appointment = await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<IBookingTransactionService>();
            return await svc.ReserveAppointmentAsync(BuildBookingRequest(seed, Utc(10), Utc(11)));
        });

        // Act — cancel from PendingPayment (no payment was made yet)
        await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<ICancellationService>();
            await svc.CancelAsync(appointment.Id, "Patient:Test", "Changed mind");
        });

        // Assert
        await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var appt = await db.Appointments
                .Include(a => a.ResourceAllocations)
                .FirstAsync(a => a.Id == appointment.Id);

            appt.Status.Should().Be(AppointmentStatus.Cancelled);
            appt.CancelledBy.Should().Be("Patient:Test");
            appt.ResourceAllocations.Should().AllSatisfy(ra =>
                ra.Status.Should().Be(AllocationStatus.Released),
                "because cancellation releases all resource allocations");

            // No refund row because appointment was never Confirmed (no payment)
            var refunds = await db.Refunds.Where(r => r.AppointmentId == appointment.Id).ToListAsync();
            refunds.Should().BeEmpty("because no payment was made for a PendingPayment appointment");
        });
    }

    [Fact]
    public async Task Cancel_FromConfirmed_CreatesRefundRow()
    {
        // Arrange — seed data and create an appointment in one scope
        var appointmentId = await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var clock = sp.GetRequiredService<IClock>();
            var seed = await TestDataSeeder.SeedBasicAsync(db, clock);

            var svc = sp.GetRequiredService<IBookingTransactionService>();
            var appt = await svc.ReserveAppointmentAsync(BuildBookingRequest(seed, Utc(10), Utc(11)));
            return appt.Id;
        });

        // Simulate payment + confirm the appointment in a fresh scope
        var paymentAttemptId = await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var clock = sp.GetRequiredService<IClock>();

            // Reload appointment in this DbContext's change tracker
            var appt = await db.Appointments.FirstAsync(a => a.Id == appointmentId);

            var payment = PaymentAttempt.Create(
                appt.Id, appt.PatientId,
                Domain.Payments.PaymentProvider.Sandbox,
                500_000m, "IRR", clock);
            await db.PaymentAttempts.AddAsync(payment);

            payment.MarkPending("SANDBOX-AUTH-1", "https://sandbox.example", clock);
            payment.MarkSucceeded("TX-001", clock);

            appt.Confirm(clock, payment.Id, 500_000m);

            // Explicitly add new audit events to the DbContext
            foreach (var evt in appt.GetUncommittedEvents())
                await db.AppointmentEvents.AddAsync(evt);
            appt.MarkEventsAsCommitted();

            await db.SaveChangesAsync();
            return payment.Id;
        });

        // Act — cancel via the CancellationService (separate scope, fresh tracking)
        await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<ICancellationService>();
            await svc.CancelAsync(appointmentId, "Patient:Test", "Schedule conflict");
        });

        // Assert
        await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var appt = await db.Appointments
                .Include(a => a.ResourceAllocations)
                .FirstAsync(a => a.Id == appointmentId);

            appt.Status.Should().Be(AppointmentStatus.Cancelled);
            appt.ResourceAllocations.Should().AllSatisfy(ra =>
                ra.Status.Should().Be(AllocationStatus.Released));

            var refund = await db.Refunds.FirstOrDefaultAsync(r => r.AppointmentId == appointmentId);
            refund.Should().NotBeNull();
            refund!.Amount.Should().Be(425_000m);
            refund.Status.Should().Be(RefundStatus.Pending);
            refund.PaymentAttemptId.Should().Be(paymentAttemptId);
        });
    }

    [Fact]
    public async Task Cancel_Twice_SecondCallThrows()
    {
        // Arrange
        var seed = await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var clock = sp.GetRequiredService<IClock>();
            return await TestDataSeeder.SeedBasicAsync(db, clock);
        });

        var appointment = await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<IBookingTransactionService>();
            return await svc.ReserveAppointmentAsync(BuildBookingRequest(seed, Utc(10), Utc(11)));
        });

        // First cancel succeeds
        await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<ICancellationService>();
            await svc.CancelAsync(appointment.Id, "Patient:Test");
        });

        // Second cancel should fail (appointment is already Cancelled)
        var act = async () => await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<ICancellationService>();
            await svc.CancelAsync(appointment.Id, "Patient:Test");
        });

        await act.Should().ThrowAsync<Domain.Exceptions.Booking.InvalidAppointmentTransitionException>();
    }

    [Fact]
    public async Task Cancel_ReleasesCapacity_AllowsSubsequentBooking()
    {
        // Arrange
        var seed = await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var clock = sp.GetRequiredService<IClock>();
            return await TestDataSeeder.SeedBasicAsync(db, clock);
        });

        // First booking [10:00, 11:00)
        var firstAppointment = await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<IBookingTransactionService>();
            return await svc.ReserveAppointmentAsync(BuildBookingRequest(seed, Utc(10), Utc(11)));
        });

        // Verify a second booking at the same time fails (slot taken)
        var act1 = async () => await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<IBookingTransactionService>();
            var db = sp.GetRequiredService<DentaraDbContext>();
            var clock = sp.GetRequiredService<IClock>();
            var secondPatient = Domain.Identity.User.Create("+989120000099", clock);
            secondPatient.VerifyPhone(clock);
            secondPatient.AssignRole(Domain.Identity.Role.Patient, clock);
            db.Users.Add(secondPatient);
            await db.SaveChangesAsync();
            var req = BuildBookingRequest(seed, Utc(10), Utc(11)) with { PatientId = secondPatient.Id };
            await svc.ReserveAppointmentAsync(req);
        });
        await act1.Should().ThrowAsync<Domain.Exceptions.Booking.SlotUnavailableException>();

        // Cancel the first booking — releases capacity
        await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<ICancellationService>();
            await svc.CancelAsync(firstAppointment.Id, "Patient:Test");
        });

        // Now a new booking at the same time should succeed
        var secondAppointment = await UsingScopeAsync(async sp =>
        {
            var svc = sp.GetRequiredService<IBookingTransactionService>();
            var db = sp.GetRequiredService<DentaraDbContext>();
            var clock = sp.GetRequiredService<IClock>();
            var secondPatient = Domain.Identity.User.Create("+989120000088", clock);
            secondPatient.VerifyPhone(clock);
            secondPatient.AssignRole(Domain.Identity.Role.Patient, clock);
            db.Users.Add(secondPatient);
            await db.SaveChangesAsync();
            var req = BuildBookingRequest(seed, Utc(10), Utc(11)) with { PatientId = secondPatient.Id };
            return await svc.ReserveAppointmentAsync(req);
        });

        secondAppointment.Should().NotBeNull();
        secondAppointment.Status.Should().Be(AppointmentStatus.PendingPayment);
    }
}
