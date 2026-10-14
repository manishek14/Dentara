namespace Dentara.UnitTests.Identity;

using Dentara.Domain.Common;
using Dentara.Domain.Identity;
using FluentAssertions;
using Xunit;

public class UserTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    [Fact]
    public void Create_WithValidPhone_SetsNormalizedPhone()
    {
        var user = User.Create("09121234567", _clock);

        user.PhoneNumberNormalized.Should().Be("+989121234567");
        user.IsActive.Should().BeTrue();
        user.IsPhoneVerified.Should().BeFalse();
        user.CreatedAtUtc.Should().Be(_clock.UtcNow);
    }

    [Theory]
    [InlineData("00989121234567", "+989121234567")]
    [InlineData("0989121234567", "+989121234567")]
    [InlineData("+989121234567", "+989121234567")]
    [InlineData("0912 123 4567", "+989121234567")]
    [InlineData("021-1234-5678", "+982112345678")] // landline, but normalization is generic
    public void NormalizePhoneNumber_HandlesVariousFormats(string input, string expected)
    {
        var user = User.Create(input, _clock);

        user.PhoneNumberNormalized.Should().Be(expected);
    }

    [Fact]
    public void AssignRole_IsIdempotent()
    {
        var user = User.Create("+989121234567", _clock);

        user.AssignRole(Role.Patient, _clock);
        user.AssignRole(Role.Patient, _clock);

        user.Roles.Should().HaveCount(1);
        user.HasRole(Role.Patient).Should().BeTrue();
    }

    [Fact]
    public void AssignRole_DifferentRoles_BothStored()
    {
        var user = User.Create("+989121234567", _clock);

        user.AssignRole(Role.Patient, _clock);
        user.AssignRole(Role.Dentist, _clock);

        user.Roles.Should().HaveCount(2);
        user.HasRole(Role.Patient).Should().BeTrue();
        user.HasRole(Role.Dentist).Should().BeTrue();
    }

    [Fact]
    public void RevokeRole_RemovesOnlyThatRole()
    {
        var user = User.Create("+989121234567", _clock);
        user.AssignRole(Role.Patient, _clock);
        user.AssignRole(Role.Dentist, _clock);

        user.RevokeRole(Role.Patient, _clock);

        user.HasRole(Role.Patient).Should().BeFalse();
        user.HasRole(Role.Dentist).Should().BeTrue();
    }

    [Fact]
    public void Deactivate_PreventsLogin()
    {
        var user = User.Create("+989121234567", _clock);

        user.Deactivate(_clock);

        user.IsActive.Should().BeFalse();
    }

    [Fact]
    public void VerifyPhone_SetsFlagAndTimestamp()
    {
        var user = User.Create("+989121234567", _clock);

        user.VerifyPhone(_clock);

        user.IsPhoneVerified.Should().BeTrue();
        user.PhoneVerifiedAtUtc.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public void Create_WithEmptyPhone_Throws()
    {
        var act = () => User.Create("", _clock);

        act.Should().Throw<ArgumentException>();
    }
}
