namespace Dentara.Domain.Notifications;

using Dentara.Domain.Common;
using Dentara.Domain.Events.Booking;
using Dentara.Domain.Events.Payments;

/// <summary>
/// Domain events for the notification module. These are raised by the booking
/// and payment modules and handled by notification event handlers to create
/// Notification records.
/// </summary>
public static class NotificationEvents
{
    /// <summary>
    /// Raised when a reminder should be scheduled for an upcoming appointment.
    /// The ReminderSchedulingWorker scans Confirmed appointments and raises
    /// this event for appointments that are ~24h away and don't have a
    /// reminder notification yet.
    /// </summary>
    public sealed record ScheduleReminderEvent(
        Guid AppointmentId,
        Guid PatientId,
        string PhoneNumber,
        DateTimeOffset AppointmentStartUtc,
        DateTimeOffset ReminderTimeUtc,
        DateTimeOffset OccurredAtUtc,
        string? CorrelationId = null)
        : DomainEvent(AppointmentId, OccurredAtUtc, CorrelationId);
}

/// <summary>
/// Raised when a notification is dispatched. Used for audit logging.
/// </summary>
public sealed record NotificationSentEvent(
    Guid NotificationId,
    Guid AppointmentId,
    string NotificationType,
    string Channel,
    DateTimeOffset SentAtUtc,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(NotificationId, OccurredAtUtc, CorrelationId);
