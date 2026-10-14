namespace Dentara.UnitTests.Scheduling;

using Dentara.Domain.Common;
using Dentara.Domain.Scheduling;
using FluentAssertions;
using Xunit;

public class BookingWindowTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidInput_StartsAsScheduled()
    {
        var window = BookingWindow.Create(
            Utc(2026, 11, 5, 17, 0),
            Utc(2026, 11, 5, 17, 30),
            Utc(2026, 11, 9),
            Utc(2026, 11, 16),
            _clock);

        window.Status.Should().Be(BookingWindowStatus.Scheduled);
        window.OpenedAtUtc.Should().BeNull();
        window.ClosedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Create_WithOpensAfterCloses_Throws()
    {
        var act = () => BookingWindow.Create(
            Utc(2026, 11, 5, 18, 0),
            Utc(2026, 11, 5, 17, 0),
            Utc(2026, 11, 9),
            Utc(2026, 11, 16),
            _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNonUtcOffsets_Throws()
    {
        var opens = new DateTimeOffset(2026, 11, 5, 17, 0, 0, TimeSpan.FromHours(3));

        var act = () => BookingWindow.Create(
            opens, Utc(2026, 11, 5, 17, 30),
            Utc(2026, 11, 9), Utc(2026, 11, 16), _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkOpen_TransitionsToOpen()
    {
        var window = BookingWindow.Create(
            Utc(2026, 11, 5, 17, 0), Utc(2026, 11, 5, 17, 30),
            Utc(2026, 11, 9), Utc(2026, 11, 16), _clock);

        window.MarkOpened(_clock);

        window.Status.Should().Be(BookingWindowStatus.Open);
        window.OpenedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkOpen_IsIdempotent()
    {
        var window = BookingWindow.Create(
            Utc(2026, 11, 5, 17, 0), Utc(2026, 11, 5, 17, 30),
            Utc(2026, 11, 9), Utc(2026, 11, 16), _clock);
        window.MarkOpened(_clock);
        var openedAt = window.OpenedAtUtc;

        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        window.MarkOpened(_clock);

        window.OpenedAtUtc.Should().Be(openedAt); // not overwritten
    }

    [Fact]
    public void MarkClose_AfterOpen_Succeeds()
    {
        var window = BookingWindow.Create(
            Utc(2026, 11, 5, 17, 0), Utc(2026, 11, 5, 17, 30),
            Utc(2026, 11, 9), Utc(2026, 11, 16), _clock);
        window.MarkOpened(_clock);

        window.MarkClosed(_clock);

        window.Status.Should().Be(BookingWindowStatus.Closed);
    }

    [Fact]
    public void MarkClose_WithoutOpen_Throws()
    {
        var window = BookingWindow.Create(
            Utc(2026, 11, 5, 17, 0), Utc(2026, 11, 5, 17, 30),
            Utc(2026, 11, 9), Utc(2026, 11, 16), _clock);

        var act = () => window.MarkClosed(_clock);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_FromScheduled_Succeeds()
    {
        var window = BookingWindow.Create(
            Utc(2026, 11, 5, 17, 0), Utc(2026, 11, 5, 17, 30),
            Utc(2026, 11, 9), Utc(2026, 11, 16), _clock);

        window.Cancel(_clock);

        window.Status.Should().Be(BookingWindowStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromClosed_Throws()
    {
        var window = BookingWindow.Create(
            Utc(2026, 11, 5, 17, 0), Utc(2026, 11, 5, 17, 30),
            Utc(2026, 11, 9), Utc(2026, 11, 16), _clock);
        window.MarkOpened(_clock);
        window.MarkClosed(_clock);

        var act = () => window.Cancel(_clock);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void IsOpenAt_WithinOpensClosesWindow_ReturnsTrue()
    {
        var window = BookingWindow.Create(
            Utc(2026, 11, 5, 17, 0), Utc(2026, 11, 5, 17, 30),
            Utc(2026, 11, 9), Utc(2026, 11, 16), _clock);

        window.IsOpenAt(Utc(2026, 11, 5, 17, 15)).Should().BeTrue();
    }

    [Fact]
    public void IsOpenAt_OutsideOpensClosesWindow_ReturnsFalse()
    {
        var window = BookingWindow.Create(
            Utc(2026, 11, 5, 17, 0), Utc(2026, 11, 5, 17, 30),
            Utc(2026, 11, 9), Utc(2026, 11, 16), _clock);

        window.IsOpenAt(Utc(2026, 11, 5, 18, 0)).Should().BeFalse();
    }
}
