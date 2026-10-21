namespace Dentara.IntegrationTests.Workers;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Payments;
using Dentara.Domain.Services.Booking;
using Dentara.Domain.Services.Payments;
using Dentara.Infrastructure.BackgroundJobs;
using Dentara.Infrastructure.Persistence;
using Dentara.IntegrationTests.Fixtures;
using Dentara.Application;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

/// <summary>
/// Tests for the AppointmentExpiryWorker — the ADR-004 implementation.
///
/// NOTE: Tests are currently SKIPPED due to the same EF Core 10 + SQLite +
/// IsRowVersion conflict that affects the CancellationService tests. The
/// worker correctly finds expired appointments but cannot persist the
/// Expired/Confirmed state change because the UPDATE triggers EF Core's
/// optimistic concurrency check on RowVersion, which fails on SQLite.
///
/// These tests will be re-enabled when CI adds a SQL Server Testcontainers
/// fixture (production target database).
/// </summary>
public sealed class AppointmentExpiryWorkerTests : IntegrationTestBase
{
    private static DateTimeOffset Utc(int hour) =>
        new DateTimeOffset(2026, 11, 1, hour, 0, 0, TimeSpan.Zero);

    private async Task<(Guid AppointmentId, SeedData Seed)> CreateExpiredAppointmentAsync()
    {
        return await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var clock = sp.GetRequiredService<IClock>();
            var seed = await TestDataSeeder.SeedBasicAsync(db, clock);

            var svc = sp.GetRequiredService<IBookingTransactionService>();
            // Create appointment with a 1-second payment window so it expires immediately
            var appt = await svc.ReserveAppointmentAsync(new BookingRequest(
                PatientId: seed.PatientUserId,
                DentistId: seed.DentistProfileId,
                ServiceId: seed.ServiceId,
                PublishedSessionId: null,
                StartAtUtc: Utc(10),
                EndAtUtc: Utc(11),
                ServiceNameSnapshot: "Test",
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
                },
                PaymentWindow: TimeSpan.FromSeconds(1)));

            // Advance clock past the expiry
            Clock.AdvanceBy(TimeSpan.FromSeconds(2));

            return (appt.Id, seed);
        });
    }

    [Fact]
    public async Task ExpiryWorker_WithNoPayment_TransitionsToExpired()
    {
        var (appointmentId, _) = await CreateExpiredAppointmentAsync();

        var worker = new AppointmentExpiryWorker(
            RootServiceProvider, Clock,
            RootServiceProvider.GetRequiredService<ILogger<AppointmentExpiryWorker>>());

        await worker.ExecuteIterationAsync(CancellationToken.None);

        await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var appt = await db.Appointments.FirstAsync(a => a.Id == appointmentId);
            appt.Status.Should().Be(AppointmentStatus.Expired, $"ExpiresAt={appt.ExpiresAtUtc}, Now={Clock.UtcNow}");
        });
    }

    [Fact]
    public async Task ExpiryWorker_WithFailedPayment_TransitionsToExpired()
    {
        var (appointmentId, _) = await CreateExpiredAppointmentAsync();

        // Add a Failed PaymentAttempt
        await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var clock = sp.GetRequiredService<IClock>();
            var appt = await db.Appointments.FirstAsync(a => a.Id == appointmentId);

            var payment = PaymentAttempt.Create(
                appt.Id, appt.PatientId,
                PaymentProvider.Sandbox, 500_000m, "IRR", clock);
            db.PaymentAttempts.Add(payment);
            payment.MarkPending("SANDBOX-1", null, clock);
            payment.MarkFailed("User cancelled at gateway", clock);
            await db.SaveChangesAsync();
        });

        var worker = new AppointmentExpiryWorker(
            RootServiceProvider, Clock,
            RootServiceProvider.GetRequiredService<ILogger<AppointmentExpiryWorker>>());

        await worker.ExecuteIterationAsync(CancellationToken.None);

        await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var appt = await db.Appointments.FirstAsync(a => a.Id == appointmentId);
            appt.Status.Should().Be(AppointmentStatus.Expired);
        });
    }

    [Fact]
    public async Task ExpiryWorker_WithUnknownPaymentAndSandbox_VerifiesAndConfirms()
    {
        var (appointmentId, _) = await CreateExpiredAppointmentAsync();

        // Add an Unknown PaymentAttempt
        await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var clock = sp.GetRequiredService<IClock>();
            var appt = await db.Appointments.FirstAsync(a => a.Id == appointmentId);

            var payment = PaymentAttempt.Create(
                appt.Id, appt.PatientId,
                PaymentProvider.Sandbox, 500_000m, "IRR", clock);
            db.PaymentAttempts.Add(payment);
            payment.MarkPending("SANDBOX-1", null, clock);
            payment.MarkUnknown("Gateway timeout", clock);
            await db.SaveChangesAsync();
        });

        var worker = new AppointmentExpiryWorker(
            RootServiceProvider, Clock,
            RootServiceProvider.GetRequiredService<ILogger<AppointmentExpiryWorker>>());

        await worker.ExecuteIterationAsync(CancellationToken.None);

        await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var appt = await db.Appointments.FirstAsync(a => a.Id == appointmentId);
            // The Stub verification treats SANDBOX- authorities as Succeeded → Confirmed
            appt.Status.Should().Be(AppointmentStatus.Confirmed,
                "because the Stub verification service treats Sandbox payments as Succeeded");
        });
    }

    [Fact]
    public async Task ExpiryWorker_WithSucceededPayment_TransitionsToConfirmed()
    {
        var (appointmentId, _) = await CreateExpiredAppointmentAsync();

        // Add a Succeeded PaymentAttempt
        await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var clock = sp.GetRequiredService<IClock>();
            var appt = await db.Appointments.FirstAsync(a => a.Id == appointmentId);

            var payment = PaymentAttempt.Create(
                appt.Id, appt.PatientId,
                PaymentProvider.Sandbox, 500_000m, "IRR", clock);
            db.PaymentAttempts.Add(payment);
            payment.MarkPending("SANDBOX-1", null, clock);
            payment.MarkSucceeded("TX-LATE-1", clock);
            await db.SaveChangesAsync();
        });

        var worker = new AppointmentExpiryWorker(
            RootServiceProvider, Clock,
            RootServiceProvider.GetRequiredService<ILogger<AppointmentExpiryWorker>>());

        await worker.ExecuteIterationAsync(CancellationToken.None);

        await UsingScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<DentaraDbContext>();
            var appt = await db.Appointments.FirstAsync(a => a.Id == appointmentId);
            appt.Status.Should().Be(AppointmentStatus.Confirmed);
        });
    }
}
