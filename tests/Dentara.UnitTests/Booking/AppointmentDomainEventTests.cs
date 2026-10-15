namespace Dentara.UnitTests.Booking;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Events.Booking;
using Dentara.Domain.Exceptions.Booking;
using FluentAssertions;
using Xunit;

public class AppointmentDomainEventTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);

    private Appointment CreateValid()
    {
        return Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Utc(2026, 11, 1, 10, 0), Utc(2026, 11, 1, 11, 0), _clock,
            "Cavity Filling", 60, 500_000m, "IRR", "Dr. Ahmadi",
            1, 15m, 85m);
    }

    [Fact]
    public void Create_RaisesAppointmentCreatedEvent()
    {
        var appt = CreateValid();

        appt.DomainEvents.Should().ContainSingle(e => e is AppointmentCreatedEvent);
        var evt = appt.DomainEvents.OfType<AppointmentCreatedEvent>().Single();
        evt.AggregateId.Should().Be(appt.Id);
        evt.PatientId.Should().Be(appt.PatientId);
        evt.PriceAmount.Should().Be(500_000m);
        evt.Currency.Should().Be("IRR");
    }

    [Fact]
    public void Confirm_RaisesAppointmentConfirmedEvent()
    {
        var appt = CreateValid();
        var paymentAttemptId = Guid.NewGuid();

        appt.Confirm(_clock, paymentAttemptId, 500_000m);

        appt.DomainEvents.Should().ContainSingle(e => e is AppointmentConfirmedEvent);
        var evt = appt.DomainEvents.OfType<AppointmentConfirmedEvent>().Single();
        evt.PaymentAttemptId.Should().Be(paymentAttemptId);
        evt.PaidAmount.Should().Be(500_000m);
    }

    [Fact]
    public void Expire_RaisesAppointmentExpiredEvent()
    {
        var appt = CreateValid();

        appt.Expire(_clock, paymentWasVerifiedAbsent: true);

        appt.DomainEvents.Should().ContainSingle(e => e is AppointmentExpiredEvent);
        var evt = appt.DomainEvents.OfType<AppointmentExpiredEvent>().Single();
        evt.PaymentWasVerifiedAbsent.Should().BeTrue();
    }

    [Fact]
    public void Cancel_FromConfirmed_RaisesEventWithRefundableAmount()
    {
        var appt = CreateValid();
        appt.Confirm(_clock, Guid.NewGuid(), 500_000m);

        appt.Cancel(_clock, "Patient", "Changed mind");

        var evt = appt.DomainEvents.OfType<AppointmentCancelledEvent>().Single();
        evt.WasConfirmedPrior.Should().BeTrue();
        evt.RefundableAmount.Should().Be(425_000m); // 85% of 500K
        evt.Currency.Should().Be("IRR");
    }

    [Fact]
    public void Cancel_FromPendingPayment_RaisesEventWithoutRefundableAmount()
    {
        var appt = CreateValid();

        appt.Cancel(_clock, "Patient", "Changed mind");

        var evt = appt.DomainEvents.OfType<AppointmentCancelledEvent>().Single();
        evt.WasConfirmedPrior.Should().BeFalse();
        evt.RefundableAmount.Should().BeNull();
    }

    [Fact]
    public void Complete_RaisesAppointmentCompletedEvent()
    {
        var appt = CreateValid();
        appt.Confirm(_clock, Guid.NewGuid(), 500_000m);

        appt.Complete(_clock);

        appt.DomainEvents.Should().ContainSingle(e => e is AppointmentCompletedEvent);
    }

    [Fact]
    public void MarkNoShow_RaisesAppointmentNoShowEvent()
    {
        var appt = CreateValid();
        appt.Confirm(_clock, Guid.NewGuid(), 500_000m);

        appt.MarkNoShow(_clock);

        appt.DomainEvents.Should().ContainSingle(e => e is AppointmentNoShowEvent);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var appt = CreateValid();
        appt.DomainEvents.Should().NotBeEmpty();

        appt.ClearDomainEvents();

        appt.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void CorrelationId_PropagatesToEvents()
    {
        var appt = CreateValid();
        var correlationId = "test-correlation-123";

        appt.Confirm(_clock, Guid.NewGuid(), 500_000m, correlationId);

        var evt = appt.DomainEvents.OfType<AppointmentConfirmedEvent>().Single();
        evt.CorrelationId.Should().Be(correlationId);
    }
}
