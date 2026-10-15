namespace Dentara.Domain.Payments;

using Dentara.Domain.Common;
using Dentara.Domain.Events.Payments;
using Dentara.Domain.Exceptions.Payments;

/// <summary>
/// A refund against a PaymentAttempt. A single payment can have multiple refunds
/// (e.g. partial refund first, then a second to complete). The aggregate invariant
/// "total refunds ≤ payment amount" is enforced in PaymentAttempt.AddRefund.
/// This is an aggregate root (in the sense that refunds have independent lifecycle).
/// </summary>
public sealed class Refund : AggregateRoot
{
    public Guid PaymentAttemptId { get; private set; }

    public Guid AppointmentId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "IRR";

    public int StatusValue { get; private set; }

    public RefundStatus Status => RefundStatus.FromValue(StatusValue);

    /// <summary>
    /// Why this refund was initiated. Examples: "Patient cancelled", "Overpayment",
    /// "Late payment after expiry (ADR-004)".
    /// </summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>
    /// Provider-side refund ID. Null until the gateway responds.
    /// </summary>
    public string? ProviderRefundId { get; private set; }

    public DateTimeOffset? RequestedAtUtc { get; private set; }

    public DateTimeOffset? SucceededAtUtc { get; private set; }

    public DateTimeOffset? FailedAtUtc { get; private set; }

    public string? FailureReason { get; private set; }

    /// <summary>
    /// Correlation id linking to AppointmentEvent for end-to-end audit.
    /// </summary>
    public string? CorrelationId { get; private set; }

    private Refund() { } // EF Core

    internal static Refund Create(
        Guid paymentAttemptId,
        Guid appointmentId,
        decimal amount,
        string currency,
        string reason,
        IClock clock,
        string? correlationId = null)
    {
        if (paymentAttemptId == Guid.Empty) throw new ArgumentException("PaymentAttemptId required.");
        if (appointmentId == Guid.Empty) throw new ArgumentException("AppointmentId required.");
        if (amount <= 0) throw new ArgumentException("Refund amount must be positive.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be ISO 4217.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Refund reason is required.");

        return new Refund
        {
            PaymentAttemptId = paymentAttemptId,
            AppointmentId = appointmentId,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            StatusValue = RefundStatus.Pending.Value,
            Reason = reason.Trim(),
            CorrelationId = correlationId,
            RequestedAtUtc = clock.UtcNow,
            CreatedAtUtc = clock.UtcNow
        };
    }

    /// <summary>
    /// Mark the refund as succeeded at the gateway. Idempotent.
    /// </summary>
    public void MarkSucceeded(string providerRefundId, IClock clock, Guid patientId, string providerName, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(providerRefundId))
            throw new ArgumentException("ProviderRefundId is required.");

        if (Status == RefundStatus.Succeeded) return; // idempotent
        if (Status != RefundStatus.Pending && Status != RefundStatus.Failed)
            throw new InvalidPaymentTransitionException(Id, Status.Name, nameof(RefundStatus.Succeeded));

        StatusValue = RefundStatus.Succeeded.Value;
        ProviderRefundId ??= providerRefundId;
        SucceededAtUtc ??= clock.UtcNow;
        FailureReason = null;
        Touch(clock.UtcNow);
        RaiseEvent(new RefundSucceededEvent(
            Id, PaymentAttemptId, AppointmentId, patientId, providerName,
            ProviderRefundId!, Amount, Currency, Reason, SucceededAtUtc.Value,
            clock.UtcNow, CorrelationId));
    }

    /// <summary>
    /// Mark the refund as failed at the gateway. Idempotent.
    /// </summary>
    public void MarkFailed(string failureReason, IClock clock, Guid patientId, string providerName)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new ArgumentException("Failure reason is required.");

        if (Status == RefundStatus.Failed) return; // idempotent
        if (Status != RefundStatus.Pending)
            throw new InvalidPaymentTransitionException(Id, Status.Name, nameof(RefundStatus.Failed));

        StatusValue = RefundStatus.Failed.Value;
        FailedAtUtc ??= clock.UtcNow;
        FailureReason = failureReason;
        Touch(clock.UtcNow);
        RaiseEvent(new RefundFailedEvent(
            Id, PaymentAttemptId, AppointmentId, patientId, providerName,
            failureReason, Amount, Currency, FailedAtUtc.Value, clock.UtcNow,
            CorrelationId));
    }
}
