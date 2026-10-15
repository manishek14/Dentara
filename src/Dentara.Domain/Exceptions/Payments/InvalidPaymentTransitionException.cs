namespace Dentara.Domain.Exceptions.Payments;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when an illegal state transition is attempted on a PaymentAttempt.
/// </summary>
public sealed class InvalidPaymentTransitionException : DomainException
{
    public string FromStatus { get; }
    public string ToStatus { get; }

    public InvalidPaymentTransitionException(Guid paymentAttemptId, string fromStatus, string toStatus)
        : base(
            "PAYMENT_INVALID_TRANSITION",
            $"Cannot transition payment {paymentAttemptId} from {fromStatus} to {toStatus}.",
            paymentAttemptId)
    {
        FromStatus = fromStatus;
        ToStatus = toStatus;
    }
}
