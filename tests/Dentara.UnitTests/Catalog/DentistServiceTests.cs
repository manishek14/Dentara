namespace Dentara.UnitTests.Catalog;

using Dentara.Domain.Catalog;
using Dentara.Domain.Common;
using FluentAssertions;
using Xunit;

public class DentistServiceTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    [Fact]
    public void Create_WithValidInput_SetsFields()
    {
        var ds = DentistService.Create(Guid.NewGuid(), Guid.NewGuid(), _clock);

        ds.IsActive.Should().BeTrue();
        ds.DurationMinutesOverride.Should().BeNull();
        ds.PriceAmountOverride.Should().BeNull();
        ds.CurrencyOverride.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyDentistId_Throws()
    {
        var act = () => DentistService.Create(Guid.Empty, Guid.NewGuid(), _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyServiceId_Throws()
    {
        var act = () => DentistService.Create(Guid.NewGuid(), Guid.Empty, _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithPriceOverrideButNoCurrency_Throws()
    {
        var act = () => DentistService.Create(
            Guid.NewGuid(), Guid.NewGuid(), _clock,
            priceAmountOverride: 200m);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithCurrencyOverrideButNoPrice_Throws()
    {
        var act = () => DentistService.Create(
            Guid.NewGuid(), Guid.NewGuid(), _clock,
            currencyOverride: "IRR");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithValidOverrides_Succeeds()
    {
        var ds = DentistService.Create(
            Guid.NewGuid(), Guid.NewGuid(), _clock,
            durationMinutesOverride: 45,
            priceAmountOverride: 750_000m,
            currencyOverride: "IRR");

        ds.DurationMinutesOverride.Should().Be(45);
        ds.PriceAmountOverride.Should().Be(750_000m);
        ds.CurrencyOverride.Should().Be("IRR");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(25 * 60)]
    public void Create_WithInvalidDurationOverride_Throws(int duration)
    {
        var act = () => DentistService.Create(
            Guid.NewGuid(), Guid.NewGuid(), _clock,
            durationMinutesOverride: duration);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ResolveDuration_WithOverride_ReturnsOverride()
    {
        var ds = DentistService.Create(
            Guid.NewGuid(), Guid.NewGuid(), _clock,
            durationMinutesOverride: 45);

        var resolved = ds.ResolveDuration(baseServiceDuration: 30);

        resolved.Should().Be(45);
    }

    [Fact]
    public void ResolveDuration_WithoutOverride_ReturnsBase()
    {
        var ds = DentistService.Create(Guid.NewGuid(), Guid.NewGuid(), _clock);

        var resolved = ds.ResolveDuration(baseServiceDuration: 30);

        resolved.Should().Be(30);
    }

    [Fact]
    public void ResolvePrice_WithOverride_ReturnsOverride()
    {
        var ds = DentistService.Create(
            Guid.NewGuid(), Guid.NewGuid(), _clock,
            priceAmountOverride: 750_000m,
            currencyOverride: "IRR");

        var (amount, currency) = ds.ResolvePrice(baseAmount: 500_000m, baseCurrency: "IRR");

        amount.Should().Be(750_000m);
        currency.Should().Be("IRR");
    }

    [Fact]
    public void ResolvePrice_WithoutOverride_ReturnsBase()
    {
        var ds = DentistService.Create(Guid.NewGuid(), Guid.NewGuid(), _clock);

        var (amount, currency) = ds.ResolvePrice(baseAmount: 500_000m, baseCurrency: "IRR");

        amount.Should().Be(500_000m);
        currency.Should().Be("IRR");
    }

    [Fact]
    public void UpdateOverrides_ReplacesValues()
    {
        var ds = DentistService.Create(
            Guid.NewGuid(), Guid.NewGuid(), _clock,
            durationMinutesOverride: 45,
            priceAmountOverride: 750_000m,
            currencyOverride: "IRR");

        ds.UpdateOverrides(_clock,
            durationMinutesOverride: 60,
            priceAmountOverride: 900_000m,
            currencyOverride: "IRR");

        ds.DurationMinutesOverride.Should().Be(60);
        ds.PriceAmountOverride.Should().Be(900_000m);
    }

    [Fact]
    public void UpdateOverrides_ToNull_ClearsOverrides()
    {
        var ds = DentistService.Create(
            Guid.NewGuid(), Guid.NewGuid(), _clock,
            durationMinutesOverride: 45,
            priceAmountOverride: 750_000m,
            currencyOverride: "IRR");

        ds.UpdateOverrides(_clock); // all null

        ds.DurationMinutesOverride.Should().BeNull();
        ds.PriceAmountOverride.Should().BeNull();
        ds.CurrencyOverride.Should().BeNull();
    }
}
