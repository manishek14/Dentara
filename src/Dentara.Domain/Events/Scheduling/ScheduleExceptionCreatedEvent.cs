namespace Dentara.Domain.Events.Scheduling;

using Dentara.Domain.Common;

/// <summary>
/// Raised when a ScheduleException is created that affects existing confirmed
/// appointments. Side effects: alert ops team for manual rescheduling.
///
/// Note: this event does NOT auto-cancel appointments. Per the architecture spec,
/// confirmed appointments affected by a schedule exception require manual review.
/// </summary>
public sealed record ScheduleExceptionCreatedEvent(
    Guid ScheduleExceptionId,
    Guid? DentistId, // null = clinic-wide
    string ExceptionType,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset EffectiveUntilUtc,
    int AffectedAppointmentsCount,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(ScheduleExceptionId, OccurredAtUtc, CorrelationId);
