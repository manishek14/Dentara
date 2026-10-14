namespace Dentara.UnitTests.Payments;

using Dentara.Domain.Common;
using Dentara.Domain.Payments;
using FluentAssertions;
using Xunit;

public class PaymentWebhookEventTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    }

    private readonly FakeClock _clock = new();

    [Fact]
    public void Create_WithValidInput_SetsFields()
    {
        var evt = PaymentWebhookEvent.Create(
            Guid.NewGuid(), PaymentProvider.Zarinpal,
            "EVT-001", "PAYMENT_SUCCEEDED",
            "{\"amount\":500000}", _clock,
            signatureHeader: "sig=abc",
            signatureVerified: true);

        evt.Provider.Should().Be(PaymentProvider.Zarinpal);
        evt.ProviderEventId.Should().Be("EVT-001");
        evt.EventType.Should().Be("PAYMENT_SUCCEEDED");
        evt.SignatureVerified.Should().BeTrue();
        evt.ProcessedSuccessfully.Should().BeFalse(); // not yet processed
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyEventId_Throws(string eventId)
    {
        var act = () => PaymentWebhookEvent.Create(
            Guid.NewGuid(), PaymentProvider.Sandbox,
            eventId, "X", "payload", _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyPayload_Throws()
    {
        var act = () => PaymentWebhookEvent.Create(
            Guid.NewGuid(), PaymentProvider.Sandbox,
            "EVT-1", "X", "", _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmptyPaymentAttemptId_Throws()
    {
        var act = () => PaymentWebhookEvent.Create(
            Guid.Empty, PaymentProvider.Sandbox,
            "EVT-1", "X", "payload", _clock);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkProcessed_SetsFlagAndNotes()
    {
        var evt = PaymentWebhookEvent.Create(
            Guid.NewGuid(), PaymentProvider.Sandbox,
            "EVT-1", "X", "payload", _clock);

        evt.MarkProcessed("Marked PaymentAttempt as Succeeded", _clock);

        evt.ProcessedSuccessfully.Should().BeTrue();
        evt.ProcessingNotes.Should().Be("Marked PaymentAttempt as Succeeded");
    }

    [Fact]
    public void MarkFailed_SetsNotes()
    {
        var evt = PaymentWebhookEvent.Create(
            Guid.NewGuid(), PaymentProvider.Sandbox,
            "EVT-1", "X", "payload", _clock);

        evt.MarkFailed("Amount mismatch: expected 500000, got 100000", _clock);

        evt.ProcessedSuccessfully.Should().BeFalse();
        evt.ProcessingNotes.Should().Contain("Amount mismatch");
    }

    [Fact]
    public void NaturalKey_CombinesProviderAndEventId()
    {
        var evt = PaymentWebhookEvent.Create(
            Guid.NewGuid(), PaymentProvider.Zarinpal,
            "EVT-001", "X", "payload", _clock);

        evt.NaturalKey.Should().Be("Zarinpal|EVT-001");
    }
}
