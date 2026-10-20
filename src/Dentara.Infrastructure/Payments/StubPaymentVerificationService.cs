namespace Dentara.Infrastructure.Payments;

using Dentara.Domain.Payments;
using Dentara.Domain.Services.Payments;
using Dentara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Stub implementation of IPaymentVerificationService. In production, this
/// would call the gateway's verify API (e.g. Zarinpal's /verify endpoint).
/// For development with the Sandbox gateway, we infer the verification outcome
/// from the PaymentAttempt's stored state.
///
/// Per ADR-004: never returns Failed unless we have a definitive answer from
/// the gateway. If the gateway is unreachable, returns StillUnknown.
/// </summary>
public sealed class StubPaymentVerificationService : IPaymentVerificationService
{
    private readonly DentaraDbContext _dbContext;
    private readonly ILogger<StubPaymentVerificationService> _logger;

    public StubPaymentVerificationService(
        DentaraDbContext dbContext,
        ILogger<StubPaymentVerificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PaymentVerificationResult> VerifyAsync(
        Guid paymentAttemptId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.PaymentAttempts
            .FirstOrDefaultAsync(p => p.Id == paymentAttemptId, cancellationToken);

        if (payment == null)
        {
            _logger.LogWarning("VerifyAsync: payment {PaymentId} not found", paymentAttemptId);
            return new PaymentVerificationResult(
                PaymentVerificationOutcome.Failed,
                null,
                "Payment attempt not found");
        }

        // If the payment is already Succeeded (e.g. webhook arrived between
        // worker iterations), return Succeeded.
        if (payment.Status == PaymentStatus.Succeeded)
        {
            return new PaymentVerificationResult(
                PaymentVerificationOutcome.Succeeded,
                payment.ProviderTransactionId,
                null);
        }

        // If the payment is definitively Failed, return Failed.
        if (payment.Status == PaymentStatus.Failed)
        {
            return new PaymentVerificationResult(
                PaymentVerificationOutcome.Failed,
                payment.ProviderTransactionId,
                payment.FailureReason);
        }

        // For Sandbox: if the gateway authority looks valid (starts with SANDBOX-),
        // treat it as Succeeded. Real gateways would call /verify here.
        if (payment.Provider == PaymentProvider.Sandbox
            && !string.IsNullOrEmpty(payment.GatewayAuthority)
            && payment.GatewayAuthority.StartsWith("SANDBOX-"))
        {
            var fakeTxId = $"TX-VERIFIED-{payment.Id:N}";
            _logger.LogInformation(
                "Sandbox verification: treating payment {PaymentId} as Succeeded", payment.Id);
            return new PaymentVerificationResult(
                PaymentVerificationOutcome.Succeeded,
                fakeTxId,
                null);
        }

        // Real gateway unreachable or sandbox authority missing — return StillUnknown.
        _logger.LogWarning(
            "VerifyAsync: payment {PaymentId} status {Status} cannot be verified; returning StillUnknown",
            payment.Id, payment.Status);
        return new PaymentVerificationResult(
            PaymentVerificationOutcome.StillUnknown,
            null,
            "Gateway verification inconclusive");
    }
}
