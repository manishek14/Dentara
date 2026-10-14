namespace Dentara.UnitTests.Catalog;

using Dentara.Domain.Catalog;
using Dentara.Domain.Common;
using FluentAssertions;
using Xunit;

public class ClinicResourceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    [Fact]
    public void Create_WithValidInput_SetsFields()
    {
        var resource = ClinicResource.Create("Dental Unit #1", ResourceType.DentalUnit, 1, _clock);

        resource.Name.Should().Be("Dental Unit #1");
        resource.Type.Should().Be(ResourceType.DentalUnit);
        resource.Capacity.Should().Be(1);
        resource.IsActive.Should().BeTrue();
        resource.OwnerDentistId.Should().BeNull();
    }

    [Fact]
    public void Create_WithOwner_AssignsOwner()
    {
        var dentistId = Guid.NewGuid();
        var resource = ClinicResource.Create(
            "Personal Chair", ResourceType.DentalUnit, 1, _clock,
            ownerDentistId: dentistId);

        resource.OwnerDentistId.Should().Be(dentistId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithInvalidCapacity_Throws(int capacity)
    {
        var act = () => ClinicResource.Create("X", ResourceType.DentalUnit, capacity, _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyName_Throws()
    {
        var act = () => ClinicResource.Create("", ResourceType.DentalUnit, 1, _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ChangeCapacity_UpdatesValue()
    {
        var resource = ClinicResource.Create("X", ResourceType.Equipment, 1, _clock);

        resource.ChangeCapacity(3, _clock);

        resource.Capacity.Should().Be(3);
    }

    [Fact]
    public void ChangeCapacity_WithZero_Throws()
    {
        var resource = ClinicResource.Create("X", ResourceType.Equipment, 1, _clock);

        var act = () => resource.ChangeCapacity(0, _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AssignOwner_SetsOwnerDentistId()
    {
        var resource = ClinicResource.Create("X", ResourceType.DentalUnit, 1, _clock);
        var dentistId = Guid.NewGuid();

        resource.AssignOwner(dentistId, _clock);

        resource.OwnerDentistId.Should().Be(dentistId);
    }

    [Fact]
    public void ClearOwner_RemovesOwnerDentistId()
    {
        var resource = ClinicResource.Create(
            "X", ResourceType.DentalUnit, 1, _clock,
            ownerDentistId: Guid.NewGuid());

        resource.ClearOwner(_clock);

        resource.OwnerDentistId.Should().BeNull();
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var resource = ClinicResource.Create("X", ResourceType.DentalUnit, 1, _clock);

        resource.Deactivate(_clock);

        resource.IsActive.Should().BeFalse();
    }
}
