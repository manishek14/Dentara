namespace Dentara.Domain.Services.Payments;

using Dentara.Domain.Payments;

/// <summary>
/// Domain service for processing webhook events from payment gateways.
/// Handles signature verification, amount validation, idempotency, and
/// triggering the appropriate state transitions on PaymentAttempt and
/// (indirectly) Appointment.
/// </summary>
public interface IWebhookProcessor
{
    /// <summary>
    /// Processes an incoming webhook event. Idempotent — re-processing the
    /// same ProviderEventId returns the original result.
    /// </summary>
    Task<WebhookResult> ProcessAsync(
        WebhookRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WebhookRequest(
    PaymentProvider Provider,
    string ProviderEventId,
    string EventType,
    string RawPayload,
    string? SignatureHeader,
    string? CorrelationId = null);

public sealed record WebhookResult(
    bool ProcessedSuccessfully,
    string? ProcessingNotes,
    bool WasDuplicate,
    Guid? PaymentAttemptId);
