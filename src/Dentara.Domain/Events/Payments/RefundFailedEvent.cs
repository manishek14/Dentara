namespace Dentara.Domain.Events.Payments;

using Dentara.Domain.Common;

/// <summary>
/// Raised when a Refund transitions to Failed at the gateway.
/// Side effects: alert ops team for manual intervention. The appointment
/// remains in Cancelled state; capacity is already released. Only the
/// financial reconciliation is pending.
/// </summary>
public sealed record RefundFailedEvent(
    Guid RefundId,
    Guid PaymentAttemptId,
    Guid AppointmentId,
    Guid PatientId,
    string Provider,
    string FailureReason,
    decimal AttemptedAmount,
    string Currency,
    DateTimeOffset FailedAtUtc,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(RefundId, OccurredAtUtc, CorrelationId);
