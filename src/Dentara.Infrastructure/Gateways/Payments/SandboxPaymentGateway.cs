namespace Dentara.Infrastructure.Gateways.Payments;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Payments;
using Microsoft.Extensions.Logging;

/// <summary>
/// Sandbox payment gateway adapter — always succeeds (or fails on demand).
/// Used for development and testing. The Sandbox provider simulates gateway
/// responses without making any HTTP calls.
///
/// For production, replace with ZarinpalGatewayAdapter (or Sadad/Sep) per
/// the user's choice. The production adapter must:
///   - Call the real gateway HTTP API.
///   - Verify callback signatures.
///   - Implement retry with exponential backoff.
/// </summary>
public sealed class SandboxPaymentGateway : IPaymentGateway
{
    private readonly ILogger<SandboxPaymentGateway> _logger;

    public SandboxPaymentGateway(ILogger<SandboxPaymentGateway> logger)
    {
        _logger = logger;
    }

    public PaymentProvider Provider => PaymentProvider.Sandbox;

    public Task<GatewayInitiationResponse> InitiateAsync(
        GatewayInitiationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sandbox gateway: initiating payment for {Amount} {Currency} (appointment {AppointmentId})",
            request.Amount, request.Currency, request.PaymentAttemptId);

        var authority = $"SANDBOX-{request.PaymentAttemptId:N}";
        var redirectUrl = $"https://sandbox.dentara.example/pay?authority={authority}";

        return Task.FromResult(new GatewayInitiationResponse(
            Success: true,
            Authority: authority,
            RedirectUrl: redirectUrl,
            FailureReason: null));
    }

    public Task<GatewayVerificationResponse> VerifyAsync(
        GatewayVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sandbox gateway: verifying authority {Authority} for {Amount} {Currency}",
            request.Authority, request.ExpectedAmount, request.ExpectedCurrency);

        // Sandbox always succeeds if the authority starts with "SANDBOX-"
        var succeeded = request.Authority.StartsWith("SANDBOX-");
        var transactionId = succeeded ? $"TX-{request.Authority}" : null;

        return Task.FromResult(new GatewayVerificationResponse(
            Succeeded: succeeded,
            DefinitivelyFailed: !succeeded,
            ProviderTransactionId: transactionId,
            FailureReason: succeeded ? null : "Invalid sandbox authority"));
    }
}
