namespace Dentara.UnitTests.Identity;

using Dentara.Domain.Common;
using Dentara.Domain.Identity;
using FluentAssertions;
using Xunit;

public class PatientProfileTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    [Fact]
    public void Create_WithValidInput_SetsFields()
    {
        var userId = Guid.NewGuid();
        var profile = PatientProfile.Create(userId, "Ali", "Rezaei", _clock);

        profile.UserId.Should().Be(userId);
        profile.FirstName.Should().Be("Ali");
        profile.LastName.Should().Be("Rezaei");
        profile.FullName.Should().Be("Ali Rezaei");
    }

    [Fact]
    public void Create_WithInvalidNationalCode_Throws()
    {
        var act = () => PatientProfile.Create(
            Guid.NewGuid(), "Ali", "Rezaei", _clock, nationalCode: "12345");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithValidNationalCode_Succeeds()
    {
        var profile = PatientProfile.Create(
            Guid.NewGuid(), "Ali", "Rezaei", _clock, nationalCode: "1234567890");

        profile.NationalCode.Should().Be("1234567890");
    }

    [Fact]
    public void Update_ChangesNameAndTouchesTimestamp()
    {
        var profile = PatientProfile.Create(Guid.NewGuid(), "Ali", "Rezaei", _clock);
        var originalUpdatedAt = profile.UpdatedAtUtc;

        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        profile.Update("Mohammad", "Hosseini", _clock);

        profile.FirstName.Should().Be("Mohammad");
        profile.LastName.Should().Be("Hosseini");
        profile.UpdatedAtUtc.Should().NotBeNull();
        profile.UpdatedAtUtc!.Value.Should().BeAfter(originalUpdatedAt ?? DateTimeOffset.MinValue);
    }
}
