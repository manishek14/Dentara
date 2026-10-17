namespace Dentara.Api.Contracts.Payments;

public sealed record InitiatePaymentRequest(
    Guid AppointmentId,
    decimal Amount,
    string Currency,
    string CallbackUrl);

public sealed record InitiatePaymentResponse(
    Guid PaymentAttemptId,
    string? RedirectUrl);

public sealed record ProcessWebhookRequest(
    string Provider, // "Sandbox" | "Zarinpal" | "Sadad" | "Sep"
    string ProviderEventId,
    string EventType,
    string RawPayload,
    string? SignatureHeader);

public sealed record ProcessWebhookResponse(
    bool WasDuplicate,
    Guid? PaymentAttemptId,
    string? ProcessingNotes);

public sealed record RequestRefundRequest(
    Guid PaymentAttemptId,
    Guid AppointmentId,
    decimal RefundAmount,
    string Currency,
    string Reason);

public sealed record RequestRefundResponse(
    Guid RefundId,
    string Status);
