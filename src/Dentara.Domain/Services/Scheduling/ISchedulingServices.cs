namespace Dentara.Domain.Services.Scheduling;

using Dentara.Domain.Scheduling;

/// <summary>
/// Domain service for the publication worker: generates PublishedSessions for
/// a BookingWindow based on each dentist's WorkSchedules and ScheduleExceptions.
/// Idempotent per ADR-006 — re-running for the same window must not produce
/// duplicate sessions.
/// </summary>
public interface IPublicationService
{
    /// <summary>
    /// Publishes sessions for the given booking window.
    /// </summary>
    /// <returns>The number of new sessions published (0 if idempotent re-run).</returns>
    Task<int> PublishAsync(
        Guid bookingWindowId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Domain service for checking dentist availability at a specific UTC time range.
/// Encapsulates the rules: WorkSchedule matches the day of week + time of day
/// (in clinic timezone), no ScheduleException covers the range.
/// </summary>
public interface IDentistAvailabilityService
{
    /// <summary>
    /// Returns true if the dentist is available for the given UTC time range
    /// according to their WorkSchedule and ScheduleExceptions.
    /// </summary>
    Task<bool> IsAvailableAsync(
        Guid dentistId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default);
}
