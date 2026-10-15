namespace Dentara.UnitTests.Specifications;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Specifications.Booking;
using FluentAssertions;
using Xunit;

public class ExpiredPendingPaymentSpecificationTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void IsSatisfiedBy_WithExpiredPendingPayment_ReturnsTrue()
    {
        // Set clock to past so ExpiresAtUtc < now
        var pastClock = new FakeClock { UtcNow = Utc(2026, 10, 14, 10, 0) };
        var appt = Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Utc(2026, 11, 1, 10, 0), Utc(2026, 11, 1, 11, 0), pastClock,
            "X", 60, 100m, "IRR", "Dr", 1, 15m, 85m);

        var now = Utc(2026, 10, 14, 10, 30); // 30 min later, ExpiresAtUtc was 10:10
        var spec = new ExpiredPendingPaymentSpecification(now);

        spec.IsSatisfiedBy(appt).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithNotYetExpiredPendingPayment_ReturnsFalse()
    {
        var appt = Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Utc(2026, 11, 1, 10, 0), Utc(2026, 11, 1, 11, 0), _clock,
            "X", 60, 100m, "IRR", "Dr", 1, 15m, 85m);

        var now = _clock.UtcNow.AddMinutes(5); // only 5 min later, ExpiresAtUtc is +10 min
        var spec = new ExpiredPendingPaymentSpecification(now);

        spec.IsSatisfiedBy(appt).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_WithConfirmedAppointment_ReturnsFalse()
    {
        var appt = Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Utc(2026, 11, 1, 10, 0), Utc(2026, 11, 1, 11, 0), _clock,
            "X", 60, 100m, "IRR", "Dr", 1, 15m, 85m);
        appt.Confirm(_clock, Guid.NewGuid(), 100m);

        var now = _clock.UtcNow.AddMinutes(30);
        var spec = new ExpiredPendingPaymentSpecification(now);

        spec.IsSatisfiedBy(appt).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_WithCancelledAppointment_ReturnsFalse()
    {
        var appt = Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Utc(2026, 11, 1, 10, 0), Utc(2026, 11, 1, 11, 0), _clock,
            "X", 60, 100m, "IRR", "Dr", 1, 15m, 85m);
        appt.Cancel(_clock, "test");

        var now = _clock.UtcNow.AddMinutes(30);
        var spec = new ExpiredPendingPaymentSpecification(now);

        spec.IsSatisfiedBy(appt).Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNonUtc_Throws()
    {
        var nonUtc = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.FromHours(3));

        var act = () => new ExpiredPendingPaymentSpecification(nonUtc);

        act.Should().Throw<ArgumentException>();
    }
}
