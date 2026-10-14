namespace Dentara.UnitTests.Booking;

using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using FluentAssertions;
using Xunit;

public class ResourceAllocationTests
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
        var apptId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var start = Utc(2026, 11, 1, 10, 0);
        var end = Utc(2026, 11, 1, 11, 0);

        var alloc = ResourceAllocationCreateHelper(apptId, resourceId, start, end, _clock);

        alloc.AppointmentId.Should().Be(apptId);
        alloc.ResourceId.Should().Be(resourceId);
        alloc.StartAtUtc.Should().Be(start);
        alloc.EndAtUtc.Should().Be(end);
        alloc.Status.Should().Be(AllocationStatus.Active);
        alloc.ReleasedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyAppointmentId_Throws()
    {
        var act = () => ResourceAllocationCreateHelper(
            Guid.Empty, Guid.NewGuid(),
            Utc(2026, 11, 1, 10, 0), Utc(2026, 11, 1, 11, 0), _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNonUtc_Throws()
    {
        var startLocal = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.FromHours(3));

        var act = () => ResourceAllocationCreateHelper(
            Guid.NewGuid(), Guid.NewGuid(),
            startLocal, Utc(2026, 11, 1, 11, 0), _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Release_FromActive_Succeeds()
    {
        var alloc = ResourceAllocationCreateHelper(
            Guid.NewGuid(), Guid.NewGuid(),
            Utc(2026, 11, 1, 10, 0), Utc(2026, 11, 1, 11, 0), _clock);

        alloc.Release(_clock, "Appointment cancelled");

        alloc.Status.Should().Be(AllocationStatus.Released);
        alloc.ReleasedAtUtc.Should().Be(_clock.UtcNow);
        alloc.ReleaseReason.Should().Be("Appointment cancelled");
    }

    [Fact]
    public void Release_IsIdempotent()
    {
        var alloc = ResourceAllocationCreateHelper(
            Guid.NewGuid(), Guid.NewGuid(),
            Utc(2026, 11, 1, 10, 0), Utc(2026, 11, 1, 11, 0), _clock);
        alloc.Release(_clock);
        var firstReleasedAt = alloc.ReleasedAtUtc;

        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        alloc.Release(_clock, "Re-release reason");

        alloc.ReleasedAtUtc.Should().Be(firstReleasedAt); // not overwritten
        alloc.ReleaseReason.Should().BeNullOrEmpty(); // not overwritten on idempotent call
    }

    [Fact]
    public void TimeRange_ReturnsCorrectRange()
    {
        var alloc = ResourceAllocationCreateHelper(
            Guid.NewGuid(), Guid.NewGuid(),
            Utc(2026, 11, 1, 10, 0), Utc(2026, 11, 1, 11, 0), _clock);

        var range = alloc.TimeRange;

        range.Duration.Should().Be(TimeSpan.FromHours(1));
    }

    /// <summary>
    /// Helper because ResourceAllocation.Create is internal (only Booking module
    /// can construct; tests in the same assembly can access it).
    /// </summary>
    private static ResourceAllocation ResourceAllocationCreateHelper(
        Guid appointmentId,
        Guid resourceId,
        DateTimeOffset start,
        DateTimeOffset end,
        IClock clock) =>
        ResourceAllocation.Create(appointmentId, resourceId, 1, start, end, clock);
}
