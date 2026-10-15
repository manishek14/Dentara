namespace Dentara.Domain.Events.Payments;

using Dentara.Domain.Common;

/// <summary>
/// Raised when a Refund transitions to Succeeded.
/// Side effects: SMS notification to patient, update accounting reports.
/// </summary>
public sealed record RefundSucceededEvent(
    Guid RefundId,
    Guid PaymentAttemptId,
    Guid AppointmentId,
    Guid PatientId,
    string Provider,
    string ProviderRefundId,
    decimal RefundedAmount,
    string Currency,
    string Reason,
    DateTimeOffset SucceededAtUtc,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(RefundId, OccurredAtUtc, CorrelationId);
