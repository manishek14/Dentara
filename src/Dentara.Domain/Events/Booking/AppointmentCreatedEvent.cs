namespace Dentara.Domain.Events.Booking;

using Dentara.Domain.Common;

/// <summary>
/// Raised when a new appointment is created (PendingPayment state).
/// Side effects: SMS confirmation to patient, optionally notify dentist.
/// </summary>
public sealed record AppointmentCreatedEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DentistId,
    Guid ServiceId,
    DateTimeOffset StartAtUtc,
    DateTimeOffset ExpiresAtUtc,
    decimal PriceAmount,
    string Currency,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(AppointmentId, OccurredAtUtc, CorrelationId);
