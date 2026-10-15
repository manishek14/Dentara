namespace Dentara.Domain.Events.Scheduling;

using Dentara.Domain.Common;

/// <summary>
/// Raised when a BookingWindow transitions from Scheduled to Open and the
/// publication worker generates PublishedSessions.
/// Side effects: notify subscribers (in v1, just log; future: push to mobile apps).
/// </summary>
public sealed record BookingWindowOpenedEvent(
    Guid BookingWindowId,
    DateTimeOffset OpensAtUtc,
    int SessionsPublished,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(BookingWindowId, OccurredAtUtc, CorrelationId);
