namespace Dentara.UnitTests.Payments;

using Dentara.Domain.Common;
using Dentara.Domain.Exceptions.Payments;
using Dentara.Domain.Payments;
using FluentAssertions;
using Xunit;

public class PaymentAttemptTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    private PaymentAttempt CreateValid()
    {
        return PaymentAttempt.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            PaymentProvider.Sandbox,
            500_000m, "IRR", _clock);
    }

    [Fact]
    public void Create_WithValidInput_SetsFields()
    {
        var pa = CreateValid();

        pa.Status.Should().Be(PaymentStatus.Initiated);
        pa.Amount.Should().Be(500_000m);
        pa.Currency.Should().Be("IRR");
        pa.Provider.Should().Be(PaymentProvider.Sandbox);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithNonPositiveAmount_Throws(decimal amount)
    {
        var act = () => PaymentAttempt.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            PaymentProvider.Sandbox, amount, "IRR", _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("IR")]
    [InlineData("IRRR")]
    public void Create_WithInvalidCurrency_Throws(string currency)
    {
        var act = () => PaymentAttempt.Create(
            Guid.NewGuid(), Guid.NewGuid(),
            PaymentProvider.Sandbox, 100m, currency, _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkPending_FromInitiated_TransitionsToPending()
    {
        var pa = CreateValid();

        pa.MarkPending("AUTH-123", "https://gateway.example/redirect", _clock);

        pa.Status.Should().Be(PaymentStatus.Pending);
        pa.GatewayAuthority.Should().Be("AUTH-123");
        pa.GatewayRedirectUrl.Should().NotBeNull();
    }

    [Fact]
    public void MarkPending_IsIdempotent()
    {
        var pa = CreateValid();
        pa.MarkPending("AUTH-123", "https://x", _clock);
        var firstInitiatedAt = pa.InitiatedAtUtc;

        _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
        pa.MarkPending("AUTH-123", "https://x", _clock);

        pa.InitiatedAtUtc.Should().Be(firstInitiatedAt);
    }

    [Fact]
    public void MarkSucceeded_FromPending_Succeeds()
    {
        var pa = CreateValid();
        pa.MarkPending("AUTH", null, _clock);

        pa.MarkSucceeded("TX-001", _clock, "Verified via callback");

        pa.Status.Should().Be(PaymentStatus.Succeeded);
        pa.ProviderTransactionId.Should().Be("TX-001");
        pa.SucceededAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void MarkSucceeded_FromUnknown_Succeeds()
    {
        // Late callback scenario (ADR-004): Unknown can transition to Succeeded
        var pa = CreateValid();
        pa.MarkPending("AUTH", null, _clock);
        pa.MarkUnknown("Gateway timeout", _clock);

        pa.MarkSucceeded("TX-001", _clock, "Late callback verified");

        pa.Status.Should().Be(PaymentStatus.Succeeded);
    }

    [Fact]
    public void MarkSucceeded_FromInitiated_Throws()
    {
        var pa = CreateValid();

        var act = () => pa.MarkSucceeded("TX-001", _clock);

        act.Should().Throw<InvalidPaymentTransitionException>();
    }

    [Fact]
    public void MarkFailed_FromPending_Succeeds()
    {
        var pa = CreateValid();
        pa.MarkPending("AUTH", null, _clock);

        pa.MarkFailed("User cancelled at gateway", _clock);

        pa.Status.Should().Be(PaymentStatus.Failed);
        pa.FailureReason.Should().NotBeNull();
    }

    [Fact]
    public void MarkFailed_FromUnknown_Throws()
    {
        var pa = CreateValid();
        pa.MarkPending("AUTH", null, _clock);
        pa.MarkUnknown("timeout", _clock);

        var act = () => pa.MarkFailed("should not happen", _clock);

        act.Should().Throw<InvalidPaymentTransitionException>();
    }

    [Fact]
    public void MarkUnknown_FromPending_Succeeds()
    {
        var pa = CreateValid();
        pa.MarkPending("AUTH", null, _clock);

        pa.MarkUnknown("Gateway did not respond", _clock);

        pa.Status.Should().Be(PaymentStatus.Unknown);
        pa.FailureReason.Should().Be("Gateway did not respond");
    }

    [Fact]
    public void TotalRefundedAmount_WithNoRefunds_ReturnsZero()
    {
        var pa = CreateValid();

        pa.TotalRefundedAmount.Should().Be(0m);
    }

    [Fact]
    public void CanRefundMore_WithinAmount_ReturnsTrue()
    {
        var pa = CreateValid();
        pa.MarkPending("AUTH", null, _clock);
        pa.MarkSucceeded("TX", _clock);

        pa.CanRefundMore(300_000m).Should().BeTrue(); // 300K of 500K
    }

    [Fact]
    public void CanRefundMore_OverAmount_ReturnsFalse()
    {
        var pa = CreateValid();
        pa.MarkPending("AUTH", null, _clock);
        pa.MarkSucceeded("TX", _clock);

        pa.CanRefundMore(600_000m).Should().BeFalse(); // 600K > 500K
    }
}
