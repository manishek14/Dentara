namespace Dentara.UnitTests.Catalog;

using Dentara.Domain.Catalog;
using Dentara.Domain.Common;
using FluentAssertions;
using Xunit;

public class ResourceServiceRequirementTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    [Fact]
    public void Create_WithValidInput_SetsFields()
    {
        var req = ResourceServiceRequirement.Create(
            Guid.NewGuid(), ResourceType.DentalUnit, 1, _clock);

        req.ResourceType.Should().Be(ResourceType.DentalUnit);
        req.QuantityRequired.Should().Be(1);
        req.RequireDentistOwned.Should().BeFalse();
        req.SpecificResourceId.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyServiceId_Throws()
    {
        var act = () => ResourceServiceRequirement.Create(
            Guid.Empty, ResourceType.DentalUnit, 1, _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithInvalidQuantity_Throws(int qty)
    {
        var act = () => ResourceServiceRequirement.Create(
            Guid.NewGuid(), ResourceType.DentalUnit, qty, _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithSpecificResource_Succeeds()
    {
        var resourceId = Guid.NewGuid();
        var req = ResourceServiceRequirement.Create(
            Guid.NewGuid(), ResourceType.Equipment, 1, _clock,
            specificResourceId: resourceId);

        req.SpecificResourceId.Should().Be(resourceId);
    }

    [Fact]
    public void Create_WithBothSpecificAndDentistOwned_Throws()
    {
        var act = () => ResourceServiceRequirement.Create(
            Guid.NewGuid(), ResourceType.DentalUnit, 1, _clock,
            requireDentistOwned: true,
            specificResourceId: Guid.NewGuid());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithMultipleAssistants_Succeeds()
    {
        var req = ResourceServiceRequirement.Create(
            Guid.NewGuid(), ResourceType.SupportStaff, 2, _clock);

        req.QuantityRequired.Should().Be(2);
    }

    [Fact]
    public void UpdateQuantity_ChangesValue()
    {
        var req = ResourceServiceRequirement.Create(
            Guid.NewGuid(), ResourceType.SupportStaff, 1, _clock);

        req.UpdateQuantity(3, _clock);

        req.QuantityRequired.Should().Be(3);
    }
}
