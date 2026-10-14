namespace Dentara.UnitTests.Scheduling;

using Dentara.Domain.Common;
using Dentara.Domain.Scheduling;
using FluentAssertions;
using Xunit;

public class PublishedSessionTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    private static DateTimeOffset Utc(int year, int month, int day, int hour = 0, int minute = 0) =>
        new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidInput_SetsFields()
    {
        var windowId = Guid.NewGuid();
        var dentistId = Guid.NewGuid();
        var start = Utc(2026, 11, 9, 10, 0);
        var end = Utc(2026, 11, 9, 11, 0);

        var session = PublishedSession.Create(windowId, dentistId, start, end, _clock);

        session.BookingWindowId.Should().Be(windowId);
        session.DentistId.Should().Be(dentistId);
        session.StartAtUtc.Should().Be(start);
        session.EndAtUtc.Should().Be(end);
        session.HasEverBeenBooked.Should().BeFalse();
        session.IdempotencyKey.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_WithEmptyBookingWindowId_Throws()
    {
        var act = () => PublishedSession.Create(
            Guid.Empty, Guid.NewGuid(),
            Utc(2026, 11, 9, 10, 0), Utc(2026, 11, 9, 11, 0), _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyDentistId_Throws()
    {
        var act = () => PublishedSession.Create(
            Guid.NewGuid(), Guid.Empty,
            Utc(2026, 11, 9, 10, 0), Utc(2026, 11, 9, 11, 0), _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithStartAfterEnd_Throws()
    {
        var act = () => PublishedSession.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            Utc(2026, 11, 9, 11, 0), Utc(2026, 11, 9, 10, 0), _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNonUtcOffset_Throws()
    {
        var start = new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.FromHours(3));

        var act = () => PublishedSession.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            start, Utc(2026, 11, 9, 11, 0), _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IdempotencyKey_IsDeterministic()
    {
        var windowId = Guid.NewGuid();
        var dentistId = Guid.NewGuid();
        var start = Utc(2026, 11, 9, 10, 0);

        var s1 = PublishedSession.Create(windowId, dentistId, start, Utc(2026, 11, 9, 11, 0), _clock);
        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        var s2 = PublishedSession.Create(windowId, dentistId, start, Utc(2026, 11, 9, 11, 0), _clock);

        s1.IdempotencyKey.Should().Be(s2.IdempotencyKey);
    }

    [Fact]
    public void IdempotencyKey_DiffersForDifferentDentists()
    {
        var windowId = Guid.NewGuid();
        var start = Utc(2026, 11, 9, 10, 0);

        var s1 = PublishedSession.Create(windowId, Guid.NewGuid(), start, Utc(2026, 11, 9, 11, 0), _clock);
        var s2 = PublishedSession.Create(windowId, Guid.NewGuid(), start, Utc(2026, 11, 9, 11, 0), _clock);

        s1.IdempotencyKey.Should().NotBe(s2.IdempotencyKey);
    }

    [Fact]
    public void MarkBooked_SetsFlag()
    {
        var session = PublishedSession.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            Utc(2026, 11, 9, 10, 0), Utc(2026, 11, 9, 11, 0), _clock);

        session.MarkBooked(_clock);

        session.HasEverBeenBooked.Should().BeTrue();
    }

    [Fact]
    public void ToTimeRange_ReturnsCorrectRange()
    {
        var session = PublishedSession.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            Utc(2026, 11, 9, 10, 0), Utc(2026, 11, 9, 11, 0), _clock);

        var range = session.ToTimeRange();

        range.StartAtUtc.Should().Be(session.StartAtUtc);
        range.EndAtUtc.Should().Be(session.EndAtUtc);
        range.Duration.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void BuildIdempotencyKey_IsStable()
    {
        var wid = Guid.NewGuid();
        var did = Guid.NewGuid();
        var start = Utc(2026, 11, 9, 10, 0);

        var key = PublishedSession.BuildIdempotencyKey(wid, did, start);

        key.Should().Contain(wid.ToString("N"));
        key.Should().Contain(did.ToString("N"));
        key.Should().Contain("2026-11-09T10:00:00.0000000+00:00");
    }
}
