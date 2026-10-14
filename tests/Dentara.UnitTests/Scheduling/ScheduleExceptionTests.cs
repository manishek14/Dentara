namespace Dentara.UnitTests.Scheduling;

using Dentara.Domain.Common;
using Dentara.Domain.Scheduling;
using FluentAssertions;
using Xunit;

public class ScheduleExceptionTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void CreateUnavailability_SetsType()
    {
        var exc = ScheduleException.CreateUnavailability(
            Guid.NewGuid(), Utc(2026, 11, 1), Utc(2026, 11, 7), _clock);

        exc.Type.Should().Be(ScheduleExceptionType.Unavailability);
        exc.WindowStartTime.Should().BeNull();
        exc.WindowEndTime.Should().BeNull();
    }

    [Fact]
    public void CreateHoliday_AppliesToAllDentists()
    {
        var exc = ScheduleException.CreateHoliday(
            Utc(2026, 12, 22), Utc(2026, 12, 23), _clock, "Yalda");

        exc.DentistId.Should().Be(Guid.Empty);
        exc.DentistIdFilter.Should().BeNull();
        exc.Type.Should().Be(ScheduleExceptionType.Holiday);
        exc.Reason.Should().Be("Yalda");
    }

    [Fact]
    public void CreateReducedHours_WithValidWindow_Succeeds()
    {
        var exc = ScheduleException.CreateReducedHours(
            Guid.NewGuid(), Utc(2026, 11, 1), Utc(2026, 11, 1, 23, 59),
            TimeSpan.FromHours(9), TimeSpan.FromHours(13), _clock);

        exc.Type.Should().Be(ScheduleExceptionType.ReducedHours);
        exc.WindowStartTime.Should().Be(TimeSpan.FromHours(9));
        exc.WindowEndTime.Should().Be(TimeSpan.FromHours(13));
    }

    [Fact]
    public void CreateAdditionalHours_WithValidWindow_Succeeds()
    {
        var exc = ScheduleException.CreateAdditionalHours(
            Guid.NewGuid(), Utc(2026, 11, 6), Utc(2026, 11, 6, 23, 59),
            TimeSpan.FromHours(18), TimeSpan.FromHours(22), _clock, "Emergency shift");

        exc.Type.Should().Be(ScheduleExceptionType.AdditionalHours);
        exc.Reason.Should().Be("Emergency shift");
    }

    [Fact]
    public void CreateUnavailability_WithReversedRange_Throws()
    {
        var act = () => ScheduleException.CreateUnavailability(
            Guid.NewGuid(), Utc(2026, 11, 7), Utc(2026, 11, 1), _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateUnavailability_WithEmptyDentistId_Throws()
    {
        var act = () => ScheduleException.CreateUnavailability(
            Guid.Empty, Utc(2026, 11, 1), Utc(2026, 11, 7), _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateReducedHours_WithReversedWindow_Throws()
    {
        var act = () => ScheduleException.CreateReducedHours(
            Guid.NewGuid(), Utc(2026, 11, 1), Utc(2026, 11, 1, 23, 59),
            TimeSpan.FromHours(13), TimeSpan.FromHours(9), _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsActiveAt_InsideRange_ReturnsTrue()
    {
        var exc = ScheduleException.CreateUnavailability(
            Guid.NewGuid(), Utc(2026, 11, 1), Utc(2026, 11, 7), _clock);

        exc.IsActiveAt(Utc(2026, 11, 3, 12, 0)).Should().BeTrue();
    }

    [Fact]
    public void IsActiveAt_OutsideRange_ReturnsFalse()
    {
        var exc = ScheduleException.CreateUnavailability(
            Guid.NewGuid(), Utc(2026, 11, 1), Utc(2026, 11, 7), _clock);

        exc.IsActiveAt(Utc(2026, 10, 30)).Should().BeFalse();
        exc.IsActiveAt(Utc(2026, 11, 8)).Should().BeFalse();
    }

    [Fact]
    public void IsActiveAt_AtBoundary_FromIsInclusive_UntilIsExclusive()
    {
        var exc = ScheduleException.CreateUnavailability(
            Guid.NewGuid(), Utc(2026, 11, 1), Utc(2026, 11, 7), _clock);

        exc.IsActiveAt(Utc(2026, 11, 1)).Should().BeTrue();
        exc.IsActiveAt(Utc(2026, 11, 7)).Should().BeFalse();
    }

    [Fact]
    public void OverlapsRange_HalfOpenSemantics()
    {
        var exc = ScheduleException.CreateUnavailability(
            Guid.NewGuid(), Utc(2026, 11, 1, 10, 0), Utc(2026, 11, 1, 12, 0), _clock);

        // Touching but not overlapping
        exc.OverlapsRange(Utc(2026, 11, 1, 12, 0), Utc(2026, 11, 1, 14, 0)).Should().BeFalse();
        // Overlapping
        exc.OverlapsRange(Utc(2026, 11, 1, 11, 0), Utc(2026, 11, 1, 13, 0)).Should().BeTrue();
    }

    [Fact]
    public void CreateUnavailability_WithNonUtcOffsets_Throws()
    {
        var from = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(3));
        var until = new DateTimeOffset(2026, 11, 7, 0, 0, 0, TimeSpan.FromHours(3));

        var act = () => ScheduleException.CreateUnavailability(Guid.NewGuid(), from, until, _clock);

        act.Should().Throw<ArgumentException>();
    }
}
