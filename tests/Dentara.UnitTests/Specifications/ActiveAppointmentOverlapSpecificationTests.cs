namespace Dentara.UnitTests.Specifications;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Specifications.Booking;
using FluentAssertions;
using Xunit;

public class ActiveAppointmentOverlapSpecificationTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    private static DateTimeOffset Utc(int hour) =>
        new DateTimeOffset(2026, 11, 1, hour, 0, 0, TimeSpan.Zero);

    private Appointment CreateAppointment(int startHour, int endHour, AppointmentStatus status)
    {
        var appt = Appointment.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Utc(startHour), Utc(endHour), _clock,
            "X", 60, 100m, "IRR", "Dr", 1, 15m, 85m);

        if (status == AppointmentStatus.Confirmed)
            appt.Confirm(_clock, Guid.NewGuid(), 100m);
        else if (status == AppointmentStatus.Expired)
            appt.Expire(_clock, paymentWasVerifiedAbsent: true);
        else if (status == AppointmentStatus.Cancelled)
            appt.Cancel(_clock, "test");

        return appt;
    }

    [Fact]
    public void IsSatisfiedBy_WithOverlappingConfirmedAppointment_ReturnsTrue()
    {
        var range = new TimeRange(Utc(10), Utc(11));
        var appt = CreateAppointment(10, 11, AppointmentStatus.Confirmed);
        var spec = new ActiveAppointmentOverlapSpecification(range);

        spec.IsSatisfiedBy(appt).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithOverlappingPendingPayment_ReturnsTrue()
    {
        var range = new TimeRange(Utc(10), Utc(11));
        var appt = CreateAppointment(10, 11, AppointmentStatus.PendingPayment);
        var spec = new ActiveAppointmentOverlapSpecification(range);

        spec.IsSatisfiedBy(appt).Should().BeTrue();
    }

    [Fact]
    public void IsSatisfiedBy_WithCancelledAppointment_ReturnsFalse()
    {
        var range = new TimeRange(Utc(10), Utc(11));
        var appt = CreateAppointment(10, 11, AppointmentStatus.Cancelled);
        var spec = new ActiveAppointmentOverlapSpecification(range);

        spec.IsSatisfiedBy(appt).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_WithExpiredAppointment_ReturnsFalse()
    {
        var range = new TimeRange(Utc(10), Utc(11));
        var appt = CreateAppointment(10, 11, AppointmentStatus.Expired);
        var spec = new ActiveAppointmentOverlapSpecification(range);

        spec.IsSatisfiedBy(appt).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_WithTouchingRange_ReturnsFalse()
    {
        // Range [11, 12) and appointment [10, 11) — they touch but don't overlap
        var range = new TimeRange(Utc(11), Utc(12));
        var appt = CreateAppointment(10, 11, AppointmentStatus.Confirmed);
        var spec = new ActiveAppointmentOverlapSpecification(range);

        spec.IsSatisfiedBy(appt).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_WithNonOverlappingRange_ReturnsFalse()
    {
        var range = new TimeRange(Utc(14), Utc(15));
        var appt = CreateAppointment(10, 11, AppointmentStatus.Confirmed);
        var spec = new ActiveAppointmentOverlapSpecification(range);

        spec.IsSatisfiedBy(appt).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_WithExcludeAppointmentId_ReturnsFalse()
    {
        var range = new TimeRange(Utc(10), Utc(11));
        var appt = CreateAppointment(10, 11, AppointmentStatus.Confirmed);
        var spec = new ActiveAppointmentOverlapSpecification(range, excludeAppointmentId: appt.Id);

        spec.IsSatisfiedBy(appt).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_WithDifferentDentist_ReturnsFalse()
    {
        var range = new TimeRange(Utc(10), Utc(11));
        var appt = CreateAppointment(10, 11, AppointmentStatus.Confirmed);
        var otherDentistId = Guid.NewGuid();
        var spec = new ActiveAppointmentOverlapSpecification(range, dentistId: otherDentistId);

        spec.IsSatisfiedBy(appt).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_WithMatchingDentist_ReturnsTrue()
    {
        var range = new TimeRange(Utc(10), Utc(11));
        var appt = CreateAppointment(10, 11, AppointmentStatus.Confirmed);
        var spec = new ActiveAppointmentOverlapSpecification(range, dentistId: appt.DentistId);

        spec.IsSatisfiedBy(appt).Should().BeTrue();
    }

    [Fact]
    public void ToExpression_ReturnsNonNullableExpression()
    {
        var range = new TimeRange(Utc(10), Utc(11));
        var spec = new ActiveAppointmentOverlapSpecification(range);

        var expr = spec.ToExpression();

        expr.Should().NotBeNull();
        // Compile and test
        var compiled = expr.Compile();
        var matchingAppt = CreateAppointment(10, 11, AppointmentStatus.Confirmed);
        compiled(matchingAppt).Should().BeTrue();
    }
}
