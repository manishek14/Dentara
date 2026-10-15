namespace Dentara.Domain.Services.Payments;

using Dentara.Domain.Payments;

/// <summary>
/// Domain service for refund processing. Initiates a refund at the gateway,
/// records the Refund row linked to the PaymentAttempt, and updates the
/// PaymentAttempt's aggregate state. Idempotent per ADR-006.
/// </summary>
public interface IRefundService
{
    /// <summary>
    /// Initiates a refund for the given payment attempt.
    /// </summary>
    Task<Refund> InitiateRefundAsync(
        Guid paymentAttemptId,
        decimal amount,
        string reason,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a refund as succeeded at the gateway. Idempotent.
    /// </summary>
    Task MarkSucceededAsync(
        Guid refundId,
        string providerRefundId,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a refund as failed at the gateway. Idempotent.
    /// </summary>
    Task MarkFailedAsync(
        Guid refundId,
        string failureReason,
        string? correlationId = null,
        CancellationToken cancellationToken = default);
}
