namespace Dentara.Domain.Exceptions.Payments;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when a webhook callback's amount or currency does not match the
/// PaymentAttempt. Indicates gateway misconfiguration or malicious replay.
/// </summary>
public sealed class WebhookAmountMismatchException : DomainException
{
    public Guid PaymentAttemptId { get; }
    public decimal ExpectedAmount { get; }
    public decimal ReceivedAmount { get; }
    public string? ExpectedCurrency { get; }
    public string? ReceivedCurrency { get; }

    public WebhookAmountMismatchException(
        Guid paymentAttemptId,
        decimal expectedAmount,
        decimal receivedAmount,
        string? expectedCurrency = null,
        string? receivedCurrency = null)
        : base(
            "PAYMENT_WEBHOOK_AMOUNT_MISMATCH",
            $"Webhook amount mismatch for payment {paymentAttemptId}: " +
            $"expected {expectedAmount} {expectedCurrency}, received {receivedAmount} {receivedCurrency}.",
            paymentAttemptId)
    {
        PaymentAttemptId = paymentAttemptId;
        ExpectedAmount = expectedAmount;
        ReceivedAmount = receivedAmount;
        ExpectedCurrency = expectedCurrency;
        ReceivedCurrency = receivedCurrency;
    }
}
