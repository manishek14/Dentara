namespace Dentara.Domain.Events.Booking;

using Dentara.Domain.Common;

/// <summary>
/// Raised when an appointment transitions to Completed (service delivered).
/// Side effects: optional patient feedback request SMS.
/// </summary>
public sealed record AppointmentCompletedEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DentistId,
    DateTimeOffset ServiceStartAtUtc,
    DateTimeOffset CompletedAtUtc,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(AppointmentId, OccurredAtUtc, CorrelationId);
