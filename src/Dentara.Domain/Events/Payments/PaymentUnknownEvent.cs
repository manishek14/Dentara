namespace Dentara.Domain.Events.Payments;

using Dentara.Domain.Common;

/// <summary>
/// Raised when a PaymentAttempt transitions to Unknown (gateway timeout/ambiguous).
/// Side effects: alert ops dashboard, schedule a verification worker run.
///
/// CRITICAL (ADR-004): Unknown is NOT Failed. The system must verify with the
/// gateway before any terminal action. This event kicks off that verification.
/// </summary>
public sealed record PaymentUnknownEvent(
    Guid PaymentAttemptId,
    Guid AppointmentId,
    Guid PatientId,
    string Provider,
    string Reason,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(PaymentAttemptId, OccurredAtUtc, CorrelationId);
