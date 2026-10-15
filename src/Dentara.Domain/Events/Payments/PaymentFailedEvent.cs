namespace Dentara.Domain.Events.Payments;

using Dentara.Domain.Common;

/// <summary>
/// Raised when a PaymentAttempt transitions to Failed (definitively).
/// Side effects: log for analytics. Does NOT expire the appointment — the
/// expiry worker handles that separately (the patient might retry payment).
/// </summary>
public sealed record PaymentFailedEvent(
    Guid PaymentAttemptId,
    Guid AppointmentId,
    Guid PatientId,
    string Provider,
    string FailureReason,
    DateTimeOffset FailedAtUtc,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(PaymentAttemptId, OccurredAtUtc, CorrelationId);
