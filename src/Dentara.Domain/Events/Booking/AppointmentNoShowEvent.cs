namespace Dentara.Domain.Events.Booking;

using Dentara.Domain.Common;

/// <summary>
/// Raised when an appointment transitions to NoShow.
/// Side effects: optional strike against patient (future booking restrictions).
/// </summary>
public sealed record AppointmentNoShowEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DentistId,
    DateTimeOffset ExpectedAtUtc,
    DateTimeOffset MarkedAtUtc,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(AppointmentId, OccurredAtUtc, CorrelationId);
