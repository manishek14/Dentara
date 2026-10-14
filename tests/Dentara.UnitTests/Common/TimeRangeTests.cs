namespace Dentara.UnitTests.Common;

using Dentara.Domain.Common;
using FluentAssertions;
using Xunit;

public class TimeRangeTests
{
    private static DateTimeOffset Utc(int year, int month, int day, int hour, int minute) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithStartAfterEnd_Throws()
    {
        var act = () => new TimeRange(Utc(2026, 1, 1, 11, 0), Utc(2026, 1, 1, 10, 0));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEqualStartAndEnd_Throws()
    {
        var act = () => new TimeRange(Utc(2026, 1, 1, 10, 0), Utc(2026, 1, 1, 10, 0));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNonUtcOffset_Throws()
    {
        var act = () => new TimeRange(
            new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.FromHours(3)));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Overlaps_WithTouchingRanges_ReturnsFalse()
    {
        // [10:00, 11:00) and [11:00, 12:00) — they touch but don't overlap
        var a = new TimeRange(Utc(2026, 1, 1, 10, 0), Utc(2026, 1, 1, 11, 0));
        var b = new TimeRange(Utc(2026, 1, 1, 11, 0), Utc(2026, 1, 1, 12, 0));

        a.Overlaps(b).Should().BeFalse();
        b.Overlaps(a).Should().BeFalse();
    }

    [Fact]
    public void Overlaps_WithOverlappingRanges_ReturnsTrue()
    {
        // [10:00, 11:00) and [10:30, 11:30)
        var a = new TimeRange(Utc(2026, 1, 1, 10, 0), Utc(2026, 1, 1, 11, 0));
        var b = new TimeRange(Utc(2026, 1, 1, 10, 30), Utc(2026, 1, 1, 11, 30));

        a.Overlaps(b).Should().BeTrue();
        b.Overlaps(a).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_WithContainedRange_ReturnsTrue()
    {
        var outer = new TimeRange(Utc(2026, 1, 1, 10, 0), Utc(2026, 1, 1, 12, 0));
        var inner = new TimeRange(Utc(2026, 1, 1, 10, 30), Utc(2026, 1, 1, 11, 30));

        outer.Overlaps(inner).Should().BeTrue();
        inner.Overlaps(outer).Should().BeTrue();
    }

    [Fact]
    public void Overlaps_WithNonOverlappingRanges_ReturnsFalse()
    {
        var a = new TimeRange(Utc(2026, 1, 1, 10, 0), Utc(2026, 1, 1, 11, 0));
        var b = new TimeRange(Utc(2026, 1, 1, 13, 0), Utc(2026, 1, 1, 14, 0));

        a.Overlaps(b).Should().BeFalse();
    }

    [Fact]
    public void Contains_WithInnerRange_ReturnsTrue()
    {
        var outer = new TimeRange(Utc(2026, 1, 1, 10, 0), Utc(2026, 1, 1, 12, 0));
        var inner = new TimeRange(Utc(2026, 1, 1, 10, 30), Utc(2026, 1, 1, 11, 30));

        outer.Contains(inner).Should().BeTrue();
    }

    [Fact]
    public void Contains_WithTouchingInner_ReturnsTrue()
    {
        var outer = new TimeRange(Utc(2026, 1, 1, 10, 0), Utc(2026, 1, 1, 12, 0));
        var touching = new TimeRange(Utc(2026, 1, 1, 10, 0), Utc(2026, 1, 1, 12, 0));

        outer.Contains(touching).Should().BeTrue();
    }

    [Fact]
    public void Duration_ReturnsCorrectTimespan()
    {
        var range = new TimeRange(Utc(2026, 1, 1, 10, 0), Utc(2026, 1, 1, 10, 30));

        range.Duration.Should().Be(TimeSpan.FromMinutes(30));
    }
}
