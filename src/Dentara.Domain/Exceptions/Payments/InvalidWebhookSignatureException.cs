namespace Dentara.Domain.Exceptions.Payments;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when a webhook callback's signature fails verification. The callback
/// is rejected without changing payment state. Stored in PaymentWebhookEvents
/// for forensic analysis.
/// </summary>
public sealed class InvalidWebhookSignatureException : DomainException
{
    public string Provider { get; }
    public string ProviderEventId { get; }

    public InvalidWebhookSignatureException(string provider, string providerEventId)
        : base(
            "PAYMENT_INVALID_WEBHOOK_SIGNATURE",
            $"Webhook signature verification failed for {provider} event {providerEventId}.")
    {
        Provider = provider;
        ProviderEventId = providerEventId;
    }
}
