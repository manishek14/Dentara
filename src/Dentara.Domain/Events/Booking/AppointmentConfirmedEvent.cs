namespace Dentara.Domain.Events.Booking;

using Dentara.Domain.Common;

/// <summary>
/// Raised when an appointment transitions to Confirmed (payment succeeded).
/// Side effects: SMS confirmation to patient, notify dentist, block calendar.
/// </summary>
public sealed record AppointmentConfirmedEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DentistId,
    Guid PaymentAttemptId,
    DateTimeOffset ConfirmedAtUtc,
    decimal PaidAmount,
    string Currency,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(AppointmentId, OccurredAtUtc, CorrelationId);
