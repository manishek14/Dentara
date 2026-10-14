namespace Dentara.UnitTests.Identity;

using Dentara.Domain.Common;
using Dentara.Domain.Identity;
using FluentAssertions;
using Xunit;

public class RoleTests
{
    [Fact]
    public void All_ReturnsFourRoles()
    {
        var all = Role.All().ToList();

        all.Should().HaveCount(4);
        all.Should().Contain(Role.Patient);
        all.Should().Contain(Role.Dentist);
        all.Should().Contain(Role.Receptionist);
        all.Should().Contain(Role.Admin);
    }

    [Theory]
    [InlineData(1, "Patient")]
    [InlineData(2, "Dentist")]
    [InlineData(3, "Receptionist")]
    [InlineData(4, "Admin")]
    public void FromValue_ReturnsExpectedRole(int value, string expectedName)
    {
        var role = Role.FromValue(value);

        role.Name.Should().Be(expectedName);
    }

    [Fact]
    public void FromValue_WithInvalidValue_Throws()
    {
        var act = () => Role.FromValue(999);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromName_IsCaseInsensitive()
    {
        var role = Role.FromName("admin");

        role.Should().Be(Role.Admin);
    }
}
