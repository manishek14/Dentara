namespace Dentara.Domain.Payments;

using Dentara.Domain.Common;

/// <summary>
/// A single attempt to pay for an appointment. An appointment may have multiple
/// attempts (user retries after a failed attempt).
///
/// CRITICAL INVARIANTS:
/// - Amount must be positive (matches Appointment.PriceAmountSnapshot).
/// - Currency is snapshotted from the appointment at creation time.
/// - ProviderTransactionId, once set, is immutable and UNIQUE within (Provider).
///   A filtered UNIQUE index with WHERE ProviderTransactionId IS NOT NULL
///   allows multiple NULL rows for attempts that never reached the gateway.
/// - State transitions are guarded by EnsureTransitionAllowed.
/// - Receiving a callback does NOT auto-transition to Succeeded. The Application
///   layer must verify the callback signature and explicitly call MarkSucceeded.
/// </summary>
public sealed class PaymentAttempt : Entity
{
    public Guid AppointmentId { get; private set; }

    /// <summary>
    /// The patient who initiated this payment. Usually the appointment's patient,
    /// but stored explicitly for audit (in case the appointment is reassigned).
    /// </summary>
    public Guid PatientId { get; private set; }

    public int ProviderValue { get; private set; }

    public PaymentProvider Provider => PaymentProvider.FromValue(ProviderValue);

    /// <summary>
    /// Amount in minor currency unit or decimal — caller decides convention.
    /// Stored as decimal for simplicity; the gateway adapter handles conversion.
    /// </summary>
    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "IRR";

    public int StatusValue { get; private set; }

    public PaymentStatus Status => PaymentStatus.FromValue(StatusValue);

    /// <summary>
    /// The order/tracking ID sent to the gateway. Set when the attempt is initiated.
    /// Each gateway has its own format (e.g. Zarinpal uses a numeric "Authority").
    /// </summary>
    public string? GatewayAuthority { get; private set; }

    /// <summary>
    /// The transaction ID returned by the gateway after a successful payment.
    /// Used for reconciliation. UNIQUE within (Provider) when non-NULL.
    /// </summary>
    public string? ProviderTransactionId { get; private set; }

    /// <summary>
    /// The URL the user should be redirected to. Set when the gateway authorizes
    /// the payment. Null for sandbox/manual flows.
    /// </summary>
    public string? GatewayRedirectUrl { get; private set; }

    public DateTimeOffset? InitiatedAtUtc { get; private set; }

    public DateTimeOffset? SucceededAtUtc { get; private set; }

    public DateTimeOffset? FailedAtUtc { get; private set; }

    public DateTimeOffset? LastCallbackAtUtc { get; private set; }

    /// <summary>
    /// Free-text error message from the gateway or our adapter.
    /// </summary>
    public string? FailureReason { get; private set; }

    /// <summary>
    /// Snapshot of the appointment's price at the time of payment attempt creation.
    /// Used for reconciliation if the appointment snapshot ever gets corrupted.
    /// </summary>
    public string? TermsSnapshotHash { get; private set; }

    private readonly List<Refund> _refunds = new();
    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    private readonly List<PaymentWebhookEvent> _webhookEvents = new();
    public IReadOnlyCollection<PaymentWebhookEvent> WebhookEvents => _webhookEvents.AsReadOnly();

    private PaymentAttempt() { } // EF Core

    public static PaymentAttempt Create(
        Guid appointmentId,
        Guid patientId,
        PaymentProvider provider,
        decimal amount,
        string currency,
        IClock clock,
        string? termsSnapshotHash = null)
    {
        if (appointmentId == Guid.Empty) throw new ArgumentException("AppointmentId required.");
        if (patientId == Guid.Empty) throw new ArgumentException("PatientId required.");
        if (amount <= 0) throw new ArgumentException("Amount must be positive.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be ISO 4217.");

        return new PaymentAttempt
        {
            AppointmentId = appointmentId,
            PatientId = patientId,
            ProviderValue = provider.Value,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            StatusValue = PaymentStatus.Initiated.Value,
            TermsSnapshotHash = termsSnapshotHash,
            CreatedAtUtc = clock.UtcNow
        };
    }

    /// <summary>
    /// Mark that the user has been redirected to the gateway. The attempt is now
    /// awaiting a callback. Idempotent if called multiple times.
    /// </summary>
    public void MarkPending(
        string gatewayAuthority,
        string? redirectUrl,
        IClock clock)
    {
        if (string.IsNullOrWhiteSpace(gatewayAuthority))
            throw new ArgumentException("GatewayAuthority is required.");

        if (Status == PaymentStatus.Pending)
        {
            // Already pending — update fields if not yet set
            GatewayAuthority ??= gatewayAuthority;
            GatewayRedirectUrl ??= redirectUrl;
            return;
        }

        if (Status != PaymentStatus.Initiated)
            throw new InvalidOperationException(
                $"Cannot transition to Pending from {Status}.");

        StatusValue = PaymentStatus.Pending.Value;
        GatewayAuthority = gatewayAuthority;
        GatewayRedirectUrl = redirectUrl;
        InitiatedAtUtc ??= clock.UtcNow;
        Touch(clock.UtcNow);
    }

    /// <summary>
    /// Mark the payment as succeeded. Only legal from Pending or Unknown.
    /// </summary>
    public void MarkSucceeded(string providerTransactionId, IClock clock, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(providerTransactionId))
            throw new ArgumentException("ProviderTransactionId is required.");

        if (Status != PaymentStatus.Pending && Status != PaymentStatus.Unknown)
            throw new InvalidOperationException(
                $"Cannot transition to Succeeded from {Status}.");

        StatusValue = PaymentStatus.Succeeded.Value;
        ProviderTransactionId ??= providerTransactionId;
        SucceededAtUtc ??= clock.UtcNow;
        LastCallbackAtUtc ??= clock.UtcNow;
        FailureReason = null;
        Touch(clock.UtcNow);
    }

    /// <summary>
    /// Mark the payment as definitively failed. Only legal from Pending.
    /// For Unknown state, use MarkUnknown() instead — never auto-fail.
    /// </summary>
    public void MarkFailed(string reason, IClock clock)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Failure reason is required.");

        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot transition to Failed from {Status}.");

        StatusValue = PaymentStatus.Failed.Value;
        FailedAtUtc ??= clock.UtcNow;
        LastCallbackAtUtc ??= clock.UtcNow;
        FailureReason = reason;
        Touch(clock.UtcNow);
    }

    /// <summary>
    /// Mark the payment as Unknown (gateway did not respond or response was ambiguous).
    /// Only legal from Pending. The expiry worker must verify before transitioning
    /// out of Unknown.
    /// </summary>
    public void MarkUnknown(string reason, IClock clock)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot transition to Unknown from {Status}.");

        StatusValue = PaymentStatus.Unknown.Value;
        FailureReason = reason;
        LastCallbackAtUtc ??= clock.UtcNow;
        Touch(clock.UtcNow);
    }

    /// <summary>
    /// Record that a webhook event was received for this payment. Does NOT
    /// change the payment status — the Application layer decides whether to
    /// transition based on the event payload.
    /// </summary>
    public void RecordWebhookEvent(PaymentWebhookEvent evt)
    {
        if (evt.PaymentAttemptId != Id)
            throw new ArgumentException("Webhook event does not belong to this payment.");
        _webhookEvents.Add(evt);
        // Note: LastCallbackAtUtc is updated by the caller if appropriate.
    }

    /// <summary>
    /// Total amount of refunds in Succeeded state for this payment.
    /// Used to enforce the invariant: total refunds ≤ payment amount.
    /// </summary>
    public decimal TotalRefundedAmount =>
        _refunds.Where(r => r.Status == RefundStatus.Succeeded).Sum(r => r.Amount);

    /// <summary>
    /// True if the payment can still be refunded (i.e. has remaining capacity).
    /// </summary>
    public bool CanRefundMore(decimal additionalAmount) =>
        Status.CanBeRefunded && TotalRefundedAmount + additionalAmount <= Amount;

    internal void AddRefund(Refund refund)
    {
        if (refund.PaymentAttemptId != Id)
            throw new ArgumentException("Refund does not belong to this payment.");
        if (!CanRefundMore(refund.Amount))
            throw new InvalidOperationException(
                $"Refund of {refund.Amount} would exceed payment amount {Amount} " +
                $"(already refunded: {TotalRefundedAmount}).");
        _refunds.Add(refund);
        UpdateRefundAggregateState();
    }

    private void UpdateRefundAggregateState()
    {
        // If any refund is pending, the payment is in RefundPending.
        if (_refunds.Any(r => r.Status == RefundStatus.Pending))
        {
            StatusValue = PaymentStatus.RefundPending.Value;
            return;
        }
        // If all refunds succeeded AND total == amount, fully refunded.
        var total = TotalRefundedAmount;
        if (total >= Amount && _refunds.Any(r => r.Status == RefundStatus.Succeeded))
        {
            StatusValue = PaymentStatus.Refunded.Value;
            return;
        }
        // If some refunds succeeded but not all amount, partially refunded.
        if (total > 0 && total < Amount)
        {
            StatusValue = PaymentStatus.PartiallyRefunded.Value;
            return;
        }
        // Otherwise, revert to Succeeded (if it was).
        if (Status == PaymentStatus.RefundPending ||
            Status == PaymentStatus.PartiallyRefunded ||
            Status == PaymentStatus.RefundFailed)
        {
            StatusValue = PaymentStatus.Succeeded.Value;
        }
    }
}
