namespace Dentara.UnitTests.Common;

using Dentara.Domain.Common;
using FluentAssertions;
using Xunit;

public class MoneyTests
{
    [Fact]
    public void Constructor_WithNegativeAmount_Throws()
    {
        var act = () => new Money(-100m, "IRR");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithInvalidCurrency_Throws()
    {
        var act1 = () => new Money(100m, "");
        var act2 = () => new Money(100m, "IRRAN");

        act1.Should().Throw<ArgumentException>();
        act2.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NormalizesCurrencyToUppercase()
    {
        var money = new Money(100m, "irr");

        money.Currency.Should().Be("IRR");
    }

    [Fact]
    public void Add_WithSameCurrency_ReturnsSum()
    {
        var a = new Money(100m, "IRR");
        var b = new Money(50m, "IRR");

        var result = a.Add(b);

        result.Amount.Should().Be(150m);
        result.Currency.Should().Be("IRR");
    }

    [Fact]
    public void Add_WithDifferentCurrency_Throws()
    {
        var a = new Money(100m, "IRR");
        var b = new Money(50m, "USD");

        var act = () => a.Add(b);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Subtract_WithSameCurrency_ReturnsDifference()
    {
        var a = new Money(100m, "IRR");
        var b = new Money(30m, "IRR");

        var result = a.Subtract(b);

        result.Amount.Should().Be(70m);
    }

    [Fact]
    public void Subtract_ResultingInNegative_Throws()
    {
        var a = new Money(30m, "IRR");
        var b = new Money(100m, "IRR");

        var act = () => a.Subtract(b);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ApplyPercent_With85Percent_Returns85PercentOfAmount()
    {
        var money = new Money(1000m, "IRR");

        var refund = money.ApplyPercent(85m);

        refund.Amount.Should().Be(850m);
    }

    [Fact]
    public void ApplyPercent_WithOver100_Throws()
    {
        var money = new Money(1000m, "IRR");

        var act = () => money.ApplyPercent(150m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
