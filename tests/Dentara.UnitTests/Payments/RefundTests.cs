namespace Dentara.UnitTests.Payments;

using Dentara.Domain.Common;
using Dentara.Domain.Payments;
using FluentAssertions;
using Xunit;

public class RefundTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    [Fact]
    public void Create_WithValidInput_SetsFields()
    {
        var refund = Refund.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            425_000m, "IRR", "Patient cancelled", _clock);

        refund.Amount.Should().Be(425_000m);
        refund.Status.Should().Be(RefundStatus.Pending);
        refund.Reason.Should().Be("Patient cancelled");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveAmount_Throws(decimal amount)
    {
        var act = () => Refund.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            amount, "IRR", "X", _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyReason_Throws()
    {
        var act = () => Refund.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            100m, "IRR", "", _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkSucceeded_FromPending_TransitionsToSucceeded()
    {
        var refund = Refund.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            100m, "IRR", "X", _clock);

        refund.MarkSucceeded("RFD-001", _clock);

        refund.Status.Should().Be(RefundStatus.Succeeded);
        refund.ProviderRefundId.Should().Be("RFD-001");
        refund.SucceededAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkSucceeded_IsIdempotent()
    {
        var refund = Refund.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            100m, "IRR", "X", _clock);
        refund.MarkSucceeded("RFD-001", _clock);
        var firstSucceededAt = refund.SucceededAtUtc;

        _clock.UtcNow = _clock.UtcNow.AddMinutes(5);
        refund.MarkSucceeded("RFD-002", _clock); // different id, should not overwrite

        refund.SucceededAtUtc.Should().Be(firstSucceededAt);
        refund.ProviderRefundId.Should().Be("RFD-001"); // not overwritten
    }

    [Fact]
    public void MarkFailed_FromPending_TransitionsToFailed()
    {
        var refund = Refund.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            100m, "IRR", "X", _clock);

        refund.MarkFailed("Bank rejected", _clock);

        refund.Status.Should().Be(RefundStatus.Failed);
        refund.FailureReason.Should().Be("Bank rejected");
    }

    [Fact]
    public void MarkFailed_FromSucceeded_Throws()
    {
        var refund = Refund.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            100m, "IRR", "X", _clock);
        refund.MarkSucceeded("RFD", _clock);

        var act = () => refund.MarkFailed("oops", _clock);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RefundStatus_IsTerminal_ForSucceededAndFailed()
    {
        RefundStatus.Succeeded.IsTerminal.Should().BeTrue();
        RefundStatus.Failed.IsTerminal.Should().BeTrue();
        RefundStatus.Pending.IsTerminal.Should().BeFalse();
    }
}
