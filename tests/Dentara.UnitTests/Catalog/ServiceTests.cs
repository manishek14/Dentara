namespace Dentara.UnitTests.Catalog;

using Dentara.Domain.Catalog;
using Dentara.Domain.Common;
using FluentAssertions;
using Xunit;

public class ServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    [Fact]
    public void Create_WithValidInput_SetsFields()
    {
        var service = Service.Create("Cavity Filling", 30, 500_000m, "IRR", _clock);

        service.Name.Should().Be("Cavity Filling");
        service.DurationMinutes.Should().Be(30);
        service.PriceAmount.Should().Be(500_000m);
        service.Currency.Should().Be("IRR");
        service.IsActive.Should().BeTrue();
        service.Version.Should().Be(1);
    }

    [Fact]
    public void Create_NormalizesCurrencyToUppercase()
    {
        var service = Service.Create("X", 10, 100m, "irr", _clock);

        service.Currency.Should().Be("IRR");
    }

    [Theory]
    [InlineData("", "Service name is required.")]
    [InlineData("   ", "Service name is required.")]
    public void Create_WithEmptyName_Throws(string name, string _)
    {
        var act = () => Service.Create(name, 30, 100m, "IRR", _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(25 * 60)] // > 24h
    public void Create_WithInvalidDuration_Throws(int duration)
    {
        var act = () => Service.Create("X", duration, 100m, "IRR", _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void Create_WithNegativePrice_Throws(decimal price)
    {
        var act = () => Service.Create("X", 30, price, "IRR", _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("IR")]
    [InlineData("IRRR")]
    public void Create_WithInvalidCurrency_Throws(string currency)
    {
        var act = () => Service.Create("X", 30, 100m, currency, _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateBaseTerms_WithChangedPrice_BumpsVersion()
    {
        var service = Service.Create("X", 30, 100m, "IRR", _clock);
        var originalVersion = service.Version;

        service.UpdateBaseTerms(30, 200m, "IRR", _clock);

        service.Version.Should().Be(originalVersion + 1);
        service.PriceAmount.Should().Be(200m);
    }

    [Fact]
    public void UpdateBaseTerms_WithSameValues_DoesNotBumpVersion()
    {
        var service = Service.Create("X", 30, 100m, "IRR", _clock);
        var originalVersion = service.Version;

        service.UpdateBaseTerms(30, 100m, "IRR", _clock);

        service.Version.Should().Be(originalVersion);
    }

    [Fact]
    public void UpdateBaseTerms_WithChangedDuration_BumpsVersion()
    {
        var service = Service.Create("X", 30, 100m, "IRR", _clock);

        service.UpdateBaseTerms(45, 100m, "IRR", _clock);

        service.Version.Should().Be(2);
        service.DurationMinutes.Should().Be(45);
    }

    [Fact]
    public void UpdateMetadata_DoesNotBumpVersion()
    {
        var service = Service.Create("X", 30, 100m, "IRR", _clock);
        var originalVersion = service.Version;

        service.UpdateMetadata("New Name", "Description", _clock);

        service.Version.Should().Be(originalVersion);
        service.Name.Should().Be("New Name");
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var service = Service.Create("X", 30, 100m, "IRR", _clock);

        service.Deactivate(_clock);

        service.IsActive.Should().BeFalse();
    }
}
