namespace Dentara.Domain.Events.Payments;

using Dentara.Domain.Common;

/// <summary>
/// Raised when a PaymentAttempt transitions to Succeeded.
/// Side effects: trigger ConfirmAppointment use case (which transitions
/// Appointment from PendingPayment to Confirmed).
///
/// CRITICAL: this event does NOT itself confirm the appointment. The handler
/// must call into the Booking module within a transaction to perform the
/// state transition. This separation enforces ADR-004's independence rule.
/// </summary>
public sealed record PaymentSucceededEvent(
    Guid PaymentAttemptId,
    Guid AppointmentId,
    Guid PatientId,
    string Provider,
    string ProviderTransactionId,
    decimal Amount,
    string Currency,
    DateTimeOffset SucceededAtUtc,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(PaymentAttemptId, OccurredAtUtc, CorrelationId);
