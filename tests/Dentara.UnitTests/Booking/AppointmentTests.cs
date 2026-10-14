namespace Dentara.UnitTests.Booking;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using FluentAssertions;
using Xunit;

public class AppointmentTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private Appointment CreateValidAppointment()
    {
        return Appointment.Create(
            patientId: Guid.NewGuid(),
            dentistId: Guid.NewGuid(),
            serviceId: Guid.NewGuid(),
            startAtUtc: Utc(2026, 11, 1, 10, 0),
            endAtUtc: Utc(2026, 11, 1, 11, 0),
            clock: _clock,
            serviceNameSnapshot: "Cavity Filling",
            serviceDurationMinutesSnapshot: 60,
            priceAmountSnapshot: 500_000m,
            currencySnapshot: "IRR",
            dentistDisplayNameSnapshot: "Dr. Ahmadi",
            cancellationPolicyVersion: 1,
            cancellationFeePercent: 15m,
            refundablePercent: 85m);
    }

    [Fact]
    public void Create_WithValidInput_SetsFields()
    {
        var appt = CreateValidAppointment();

        appt.Status.Should().Be(AppointmentStatus.PendingPayment);
        appt.ExpiresAtUtc.Should().Be(_clock.UtcNow.AddMinutes(10));
        appt.ServiceNameSnapshot.Should().Be("Cavity Filling");
        appt.PriceAmountSnapshot.Should().Be(500_000m);
        appt.RefundablePercentSnapshot.Should().Be(85m);
        appt.CancellationFeePercentSnapshot.Should().Be(15m);
    }

    [Fact]
    public void Create_RecordsCreatedAndPaymentWindowEvents()
    {
        var appt = CreateValidAppointment();

        appt.Events.Should().HaveCount(2);
        appt.Events.Should().Contain(e => e.EventType == AppointmentEventType.Created);
        appt.Events.Should().Contain(e => e.EventType == AppointmentEventType.PaymentWindowStarted);
    }

    [Fact]
    public void Create_WithNonUtcStart_Throws()
    {
        var startLocal = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.FromHours(3));

        var act = () => Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            startLocal, Utc(2026, 11, 1, 11, 0), _clock,
            "X", 60, 100m, "IRR", "Dr", 1, 15m, 85m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithStartAfterEnd_Throws()
    {
        var act = () => Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Utc(2026, 11, 1, 11, 0), Utc(2026, 11, 1, 10, 0), _clock,
            "X", 60, 100m, "IRR", "Dr", 1, 15m, 85m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithFeePlusRefundableNot100_Throws()
    {
        var act = () => Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Utc(2026, 11, 1, 10, 0), Utc(2026, 11, 1, 11, 0), _clock,
            "X", 60, 100m, "IRR", "Dr", 1, 10m, 85m); // 10 + 85 = 95, not 100

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithCustomPaymentWindow_SetsExpiry()
    {
        var appt = Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Utc(2026, 11, 1, 10, 0), Utc(2026, 11, 1, 11, 0), _clock,
            "X", 60, 100m, "IRR", "Dr", 1, 15m, 85m,
            paymentWindow: TimeSpan.FromMinutes(15));

        appt.ExpiresAtUtc.Should().Be(_clock.UtcNow.AddMinutes(15));
    }

    [Fact]
    public void Confirm_FromPendingPayment_TransitionsToConfirmed()
    {
        var appt = CreateValidAppointment();

        appt.Confirm(_clock);

        appt.Status.Should().Be(AppointmentStatus.Confirmed);
        appt.ConfirmedAtUtc.Should().Be(_clock.UtcNow);
        appt.Events.Should().Contain(e => e.EventType == AppointmentEventType.Confirmed);
    }

    [Fact]
    public void Confirm_FromConfirmed_Throws()
    {
        var appt = CreateValidAppointment();
        appt.Confirm(_clock);

        var act = () => appt.Confirm(_clock);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Expire_FromPendingPayment_TransitionsToExpired()
    {
        var appt = CreateValidAppointment();

        appt.Expire(_clock);

        appt.Status.Should().Be(AppointmentStatus.Expired);
        appt.ExpiredAtUtc.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public void Expire_FromConfirmed_Throws()
    {
        var appt = CreateValidAppointment();
        appt.Confirm(_clock);

        var act = () => appt.Expire(_clock);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_FromPendingPayment_Succeeds()
    {
        var appt = CreateValidAppointment();

        appt.Cancel(_clock, "Patient", "Changed mind");

        appt.Status.Should().Be(AppointmentStatus.Cancelled);
        appt.CancelledBy.Should().Be("Patient");
        appt.CancellationReason.Should().Be("Changed mind");
    }

    [Fact]
    public void Cancel_FromConfirmed_Succeeds()
    {
        var appt = CreateValidAppointment();
        appt.Confirm(_clock);

        appt.Cancel(_clock, "Patient");

        appt.Status.Should().Be(AppointmentStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromExpired_Throws()
    {
        var appt = CreateValidAppointment();
        appt.Expire(_clock);

        var act = () => appt.Cancel(_clock, "Patient");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_FromCancelled_IsIdempotentViaException()
    {
        var appt = CreateValidAppointment();
        appt.Cancel(_clock, "Patient");

        var act = () => appt.Cancel(_clock, "Patient");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_FromConfirmed_Succeeds()
    {
        var appt = CreateValidAppointment();
        appt.Confirm(_clock);

        appt.Complete(_clock, "Service delivered");

        appt.Status.Should().Be(AppointmentStatus.Completed);
        appt.CompletedAtUtc.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public void Complete_FromPendingPayment_Throws()
    {
        var appt = CreateValidAppointment();

        var act = () => appt.Complete(_clock);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkNoShow_FromConfirmed_Succeeds()
    {
        var appt = CreateValidAppointment();
        appt.Confirm(_clock);

        appt.MarkNoShow(_clock, "Patient did not arrive");

        appt.Status.Should().Be(AppointmentStatus.NoShow);
        appt.NoShowAtUtc.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public void CalculateRefundAmount_UsesSnapshotPercent()
    {
        var appt = CreateValidAppointment();
        appt.Confirm(_clock);

        var refund = appt.CalculateRefundAmount();

        refund.Should().Be(500_000m * 0.85m); // 425_000
    }

    [Fact]
    public void CalculateCancellationFee_UsesSnapshotPercent()
    {
        var appt = CreateValidAppointment();
        appt.Confirm(_clock);

        var fee = appt.CalculateCancellationFee();

        fee.Should().Be(500_000m * 0.15m); // 75_000
    }

    [Fact]
    public void TimeRange_ReturnsCorrectRange()
    {
        var appt = CreateValidAppointment();

        var range = appt.TimeRange;

        range.StartAtUtc.Should().Be(appt.StartAtUtc);
        range.EndAtUtc.Should().Be(appt.EndAtUtc);
        range.Duration.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Confirm_IsIdempotentForTimestamp()
    {
        var appt = CreateValidAppointment();
        appt.Confirm(_clock);
        var firstConfirmedAt = appt.ConfirmedAtUtc;

        // Try to confirm again — should throw (not idempotent at state level)
        var act = () => appt.Confirm(_clock);
        act.Should().Throw<InvalidOperationException>();

        appt.ConfirmedAtUtc.Should().Be(firstConfirmedAt); // unchanged
    }
}
