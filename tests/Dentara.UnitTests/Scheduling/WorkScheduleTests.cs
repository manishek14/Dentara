namespace Dentara.UnitTests.Scheduling;

using Dentara.Domain.Common;
using Dentara.Domain.Scheduling;
using FluentAssertions;
using Xunit;

public class WorkScheduleTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    [Fact]
    public void Create_WithValidInput_SetsFields()
    {
        var schedule = WorkSchedule.Create(
            Guid.NewGuid(),
            DayOfWeek.Saturday,
            TimeSpan.FromHours(9),
            TimeSpan.FromHours(13),
            _clock);

        schedule.DayOfWeek.Should().Be(DayOfWeek.Saturday);
        schedule.StartTime.Should().Be(TimeSpan.FromHours(9));
        schedule.EndTime.Should().Be(TimeSpan.FromHours(13));
        schedule.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmptyDentistId_Throws()
    {
        var act = () => WorkSchedule.Create(
            Guid.Empty, DayOfWeek.Saturday,
            TimeSpan.FromHours(9), TimeSpan.FromHours(13), _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(9, 9)]   // equal
    [InlineData(13, 9)]  // start > end
    public void Create_WithInvalidTimeRange_Throws(int startHour, int endHour)
    {
        var act = () => WorkSchedule.Create(
            Guid.NewGuid(), DayOfWeek.Saturday,
            TimeSpan.FromHours(startHour), TimeSpan.FromHours(endHour), _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsEffectiveAt_RespectsValidityWindow()
    {
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var until = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);
        var schedule = WorkSchedule.Create(
            Guid.NewGuid(), DayOfWeek.Monday,
            TimeSpan.FromHours(9), TimeSpan.FromHours(17),
            _clock, effectiveFromUtc: from, effectiveUntilUtc: until);

        schedule.IsEffectiveAt(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero)).Should().BeTrue();
        schedule.IsEffectiveAt(new DateTimeOffset(2025, 6, 1, 12, 0, 0, TimeSpan.Zero)).Should().BeFalse();
        schedule.IsEffectiveAt(new DateTimeOffset(2027, 6, 1, 12, 0, 0, TimeSpan.Zero)).Should().BeFalse();
    }

    [Fact]
    public void IsEffectiveAt_WhenInactive_ReturnsFalse()
    {
        var schedule = WorkSchedule.Create(
            Guid.NewGuid(), DayOfWeek.Monday,
            TimeSpan.FromHours(9), TimeSpan.FromHours(17), _clock);

        schedule.Deactivate(_clock);

        schedule.IsEffectiveAt(_clock.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void UpdateTimes_ChangesValues()
    {
        var schedule = WorkSchedule.Create(
            Guid.NewGuid(), DayOfWeek.Monday,
            TimeSpan.FromHours(9), TimeSpan.FromHours(17), _clock);

        schedule.UpdateTimes(TimeSpan.FromHours(10), TimeSpan.FromHours(15), _clock);

        schedule.StartTime.Should().Be(TimeSpan.FromHours(10));
        schedule.EndTime.Should().Be(TimeSpan.FromHours(15));
    }

    [Fact]
    public void ToDailyTimeWindow_ReturnsCorrectWindow()
    {
        var schedule = WorkSchedule.Create(
            Guid.NewGuid(), DayOfWeek.Wednesday,
            TimeSpan.FromHours(14), TimeSpan.FromHours(20), _clock);

        var window = schedule.ToDailyTimeWindow();

        window.DayOfWeek.Should().Be(DayOfWeek.Wednesday);
        window.StartTime.Should().Be(TimeSpan.FromHours(14));
        window.EndTime.Should().Be(TimeSpan.FromHours(20));
    }

    [Fact]
    public void SetValidityWindow_WithReversedRange_Throws()
    {
        var schedule = WorkSchedule.Create(
            Guid.NewGuid(), DayOfWeek.Monday,
            TimeSpan.FromHours(9), TimeSpan.FromHours(17), _clock);
        var from = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);
        var until = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var act = () => schedule.SetValidityWindow(from, until, _clock);

        act.Should().Throw<ArgumentException>();
    }
}
