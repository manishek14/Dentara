namespace Dentara.Domain.Exceptions.Payments;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when a refund amount would exceed the total paid amount.
/// Invariant: SUM(refunds.Succeeded) ≤ PaymentAttempt.Amount.
/// </summary>
public sealed class RefundExceedsPaymentException : DomainException
{
    public Guid PaymentAttemptId { get; }
    public decimal PaymentAmount { get; }
    public decimal AlreadyRefunded { get; }
    public decimal AttemptedRefund { get; }

    public RefundExceedsPaymentException(
        Guid paymentAttemptId,
        decimal paymentAmount,
        decimal alreadyRefunded,
        decimal attemptedRefund)
        : base(
            "PAYMENT_REFUND_EXCEEDS_TOTAL",
            $"Refund of {attemptedRefund} would exceed payment total {paymentAmount} " +
            $"(already refunded: {alreadyRefunded}).",
            paymentAttemptId)
    {
        PaymentAttemptId = paymentAttemptId;
        PaymentAmount = paymentAmount;
        AlreadyRefunded = alreadyRefunded;
        AttemptedRefund = attemptedRefund;
    }
}
