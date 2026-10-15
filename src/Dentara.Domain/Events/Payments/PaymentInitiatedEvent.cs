namespace Dentara.Domain.Events.Payments;

using Dentara.Domain.Common;

/// <summary>
/// Raised when a PaymentAttempt transitions to Pending (user redirected to gateway).
/// Side effects: none (informational). Useful for monitoring abandonment rates.
/// </summary>
public sealed record PaymentInitiatedEvent(
    Guid PaymentAttemptId,
    Guid AppointmentId,
    Guid PatientId,
    string Provider,
    decimal Amount,
    string Currency,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(PaymentAttemptId, OccurredAtUtc, CorrelationId);
