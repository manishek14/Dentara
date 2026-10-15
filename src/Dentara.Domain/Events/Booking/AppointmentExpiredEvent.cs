namespace Dentara.Domain.Events.Booking;

using Dentara.Domain.Common;

/// <summary>
/// Raised when an appointment's payment window expires.
/// Side effects: capacity released, patient SMS notification.
///
/// CRITICAL (ADR-004): the expiry worker must verify payment status is NOT
/// Unknown or Succeeded before raising this event. If payment is Unknown,
/// the worker must wait until the gateway responds.
/// </summary>
public sealed record AppointmentExpiredEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DentistId,
    DateTimeOffset ExpiresAtUtc,
    bool PaymentWasVerifiedAbsent, // worker assertion: payment is truly absent
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(AppointmentId, OccurredAtUtc, CorrelationId);
