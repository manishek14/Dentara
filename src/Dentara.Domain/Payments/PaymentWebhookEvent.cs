namespace Dentara.Domain.Payments;

using Dentara.Domain.Common;

/// <summary>
/// Raw record of a webhook/callback event received from a payment gateway.
///
/// PURPOSE: idempotency + audit. The (Provider, ProviderEventId) pair is UNIQUE,
/// so if the gateway re-sends the same event, the INSERT fails and the Application
/// layer can return 200 OK without re-processing.
///
/// CRITICAL: a webhook event is RAW DATA. It does NOT change PaymentAttempt.Status.
/// The Application layer must:
///   1. Verify the event signature.
///   2. Validate the event's amount/currency/orderId match the PaymentAttempt.
///   3. If valid, transition PaymentAttempt (e.g. MarkSucceeded).
///   4. If invalid, log the event for investigation but do not transition.
/// </summary>
public sealed class PaymentWebhookEvent : Entity
{
    public Guid PaymentAttemptId { get; private set; }

    public int ProviderValue { get; private set; }

    public PaymentProvider Provider => PaymentProvider.FromValue(ProviderValue);

    /// <summary>
    /// The event ID provided by the gateway. Each gateway guarantees uniqueness
    /// within its scope. Combined with Provider, forms a UNIQUE constraint.
    /// </summary>
    public string ProviderEventId { get; private set; } = string.Empty;

    /// <summary>
    /// Event type as reported by the gateway (e.g. "PAYMENT_SUCCEEDED", "REFUND_COMPLETED").
    /// Free-text — each provider has its own taxonomy.
    /// </summary>
    public string EventType { get; private set; } = string.Empty;

    public DateTimeOffset ReceivedAtUtc { get; private set; }

    /// <summary>
    /// Raw payload as received (JSON or form-encoded). Stored as-is for audit.
    /// </summary>
    public string RawPayload { get; private set; } = string.Empty;

    /// <summary>
    /// The gateway's signature header. Stored for forensic analysis.
    /// </summary>
    public string? SignatureHeader { get; private set; }

    /// <summary>
    /// True if the event's signature was verified successfully.
    /// </summary>
    public bool SignatureVerified { get; private set; }

    /// <summary>
    /// True if the event was successfully processed (i.e. caused a state transition
    /// or was determined to be a no-op duplicate). False if it failed validation.
    /// </summary>
    public bool ProcessedSuccessfully { get; private set; }

    /// <summary>
    /// Free-text processing notes (e.g. "Marked PaymentAttempt as Succeeded",
    /// "Ignored: amount mismatch").
    /// </summary>
    public string? ProcessingNotes { get; private set; }

    /// <summary>
    /// Correlation id linking to AppointmentEvent.
    /// </summary>
    public string? CorrelationId { get; private set; }

    private PaymentWebhookEvent() { } // EF Core

    public static PaymentWebhookEvent Create(
        Guid paymentAttemptId,
        PaymentProvider provider,
        string providerEventId,
        string eventType,
        string rawPayload,
        IClock clock,
        string? signatureHeader = null,
        bool signatureVerified = false,
        string? correlationId = null)
    {
        if (paymentAttemptId == Guid.Empty) throw new ArgumentException("PaymentAttemptId required.");
        if (string.IsNullOrWhiteSpace(providerEventId))
            throw new ArgumentException("ProviderEventId is required.");
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException("EventType is required.");
        if (string.IsNullOrWhiteSpace(rawPayload))
            throw new ArgumentException("RawPayload is required.");

        return new PaymentWebhookEvent
        {
            PaymentAttemptId = paymentAttemptId,
            ProviderValue = provider.Value,
            ProviderEventId = providerEventId.Trim(),
            EventType = eventType.Trim(),
            RawPayload = rawPayload,
            SignatureHeader = signatureHeader,
            SignatureVerified = signatureVerified,
            ReceivedAtUtc = clock.UtcNow,
            CorrelationId = correlationId,
            CreatedAtUtc = clock.UtcNow
        };
    }

    public void MarkProcessed(string notes, IClock clock)
    {
        ProcessedSuccessfully = true;
        ProcessingNotes = notes;
        Touch(clock.UtcNow);
    }

    public void MarkFailed(string reason, IClock clock)
    {
        ProcessedSuccessfully = false;
        ProcessingNotes = reason;
        Touch(clock.UtcNow);
    }

    /// <summary>
    /// Returns the natural key used for the UNIQUE constraint.
    /// </summary>
    public string NaturalKey => $"{Provider.Name}|{ProviderEventId}";
}
