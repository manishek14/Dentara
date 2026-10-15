namespace Dentara.Domain.Services.Payments;

using Dentara.Domain.Payments;

/// <summary>
/// Domain service for verifying the status of a payment with the gateway.
/// Used by the expiry worker (ADR-004 critical scenario) before transitioning
/// a PendingPayment appointment to Expired.
///
/// CRITICAL: never treat Unknown as Failed. The worker must call this service
/// to verify with the gateway. If the gateway confirms success, the worker
/// triggers ConfirmAppointment. If the gateway confirms failure, the worker
/// triggers Expire. If the gateway is unreachable, the worker leaves the
/// payment in Unknown state and retries later.
/// </summary>
public interface IPaymentVerificationService
{
    /// <summary>
    /// Verifies the status of a payment attempt with the gateway.
    /// </summary>
    Task<PaymentVerificationResult> VerifyAsync(
        Guid paymentAttemptId,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentVerificationResult(
    PaymentVerificationOutcome Outcome,
    string? ProviderTransactionId,
    string? FailureReason);

public enum PaymentVerificationOutcome
{
    /// <summary>
    /// Gateway confirmed the payment succeeded. Caller should trigger ConfirmAppointment.
    /// </summary>
    Succeeded,

    /// <summary>
    /// Gateway confirmed the payment failed. Caller should trigger Appointment.Expire.
    /// </summary>
    Failed,

    /// <summary>
    /// Gateway is unreachable or returned an ambiguous response. Caller must
    /// leave the payment in Unknown state and retry later.
    /// </summary>
    StillUnknown
}
