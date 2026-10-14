namespace Dentara.UnitTests.Payments;

using Dentara.Domain.Common;
using Dentara.Domain.Payments;
using FluentAssertions;
using Xunit;

public class PaymentStatusTests
{
    [Fact]
    public void All_ReturnsNineStatuses()
    {
        var all = PaymentStatus.All().ToList();
        all.Should().HaveCount(9);
    }

    [Theory]
    [InlineData(1, "Initiated")]
    [InlineData(2, "Pending")]
    [InlineData(3, "Succeeded")]
    [InlineData(4, "Failed")]
    [InlineData(5, "Unknown")]
    [InlineData(6, "RefundPending")]
    [InlineData(7, "PartiallyRefunded")]
    [InlineData(8, "Refunded")]
    [InlineData(9, "RefundFailed")]
    public void FromValue_ReturnsExpectedStatus(int value, string expectedName)
    {
        PaymentStatus.FromValue(value).Name.Should().Be(expectedName);
    }

    [Theory]
    [InlineData(4, true)]   // Failed
    [InlineData(8, true)]   // Refunded
    [InlineData(1, false)]  // Initiated
    [InlineData(2, false)]  // Pending
    [InlineData(3, false)]  // Succeeded
    [InlineData(5, false)]  // Unknown
    [InlineData(6, false)]  // RefundPending
    [InlineData(7, false)]  // PartiallyRefunded
    [InlineData(9, false)]  // RefundFailed
    public void IsTerminal_IsCorrect(int value, bool expected)
    {
        PaymentStatus.FromValue(value).IsTerminal.Should().Be(expected);
    }

    [Theory]
    [InlineData(3, true)]   // Succeeded
    [InlineData(7, true)]   // PartiallyRefunded
    [InlineData(1, false)]  // Initiated
    [InlineData(4, false)]  // Failed
    [InlineData(8, false)]  // Refunded
    public void CanBeRefunded_IsCorrect(int value, bool expected)
    {
        PaymentStatus.FromValue(value).CanBeRefunded.Should().Be(expected);
    }

    [Theory]
    [InlineData(6, true)]   // RefundPending
    [InlineData(9, true)]   // RefundFailed
    [InlineData(3, false)]  // Succeeded
    [InlineData(7, false)]  // PartiallyRefunded
    [InlineData(8, false)]  // Refunded
    public void IsRefundInFlight_IsCorrect(int value, bool expected)
    {
        PaymentStatus.FromValue(value).IsRefundInFlight.Should().Be(expected);
    }
}
