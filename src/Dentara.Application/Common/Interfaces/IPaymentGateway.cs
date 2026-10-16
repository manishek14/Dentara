namespace Dentara.Application.Common.Interfaces;

using Dentara.Domain.Payments;

/// <summary>
/// Payment gateway abstraction. The Application layer uses this to initiate
/// payments and verify their status. The Infrastructure layer provides
/// concrete adapters: SandboxGatewayAdapter (for tests/dev) and
/// ZarinpalGatewayAdapter (for production, per user choice).
/// </summary>
public interface IPaymentGateway
{
    PaymentProvider Provider { get; }

    /// <summary>
    /// Initiates a payment at the gateway. Returns the authority token and
    /// the URL the user should be redirected to. Does NOT modify the DB.
    /// </summary>
    Task<GatewayInitiationResponse> InitiateAsync(
        GatewayInitiationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a payment with the gateway. Used by the expiry worker (ADR-004)
    /// before transitioning an appointment to Expired, and by the webhook
    /// processor as a secondary verification step.
    /// </summary>
    Task<GatewayVerificationResponse> VerifyAsync(
        GatewayVerificationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record GatewayInitiationRequest(
    Guid PaymentAttemptId,
    decimal Amount,
    string Currency,
    string CallbackUrl,
    string Description);

public sealed record GatewayInitiationResponse(
    bool Success,
    string? Authority,
    string? RedirectUrl,
    string? FailureReason);

public sealed record GatewayVerificationRequest(
    string Authority,
    decimal ExpectedAmount,
    string ExpectedCurrency);

public sealed record GatewayVerificationResponse(
    bool Succeeded,
    bool DefinitivelyFailed, // false means "Unknown"
    string? ProviderTransactionId,
    string? FailureReason);
