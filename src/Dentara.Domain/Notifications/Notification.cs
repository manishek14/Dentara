namespace Dentara.Domain.Notifications;

using Dentara.Domain.Common;

/// <summary>
/// A notification record — tracks the intent to send a message, the delivery
/// status, and the result. Notifications are created by domain event handlers
/// and dispatched by the NotificationDispatcherWorker.
///
/// Per the architecture spec: logs include tracking ids but MUST NOT include
/// OTP, payment-sensitive info, or secrets. The MessageBody is the rendered
/// template output (no secrets), not the raw domain data.
/// </summary>
public sealed class Notification : Entity
{
    public Guid AppointmentId { get; private set; }
    public Guid PatientId { get; private set; }

    public int TypeValue { get; private set; }
    public NotificationType Type => NotificationType.FromValue(TypeValue);

    public int ChannelValue { get; private set; }
    public NotificationChannel Channel => NotificationChannel.FromValue(ChannelValue);

    public int StatusValue { get; private set; }
    public NotificationStatus Status => NotificationStatus.FromValue(StatusValue);

    /// <summary>
    /// The patient's phone number (E.164) at the time of notification creation.
    /// Stored separately so that phone number changes don't break delivery.
    /// </summary>
    public string PhoneNumber { get; private set; } = string.Empty;

    /// <summary>
    /// The rendered message body (no secrets, no raw domain data).
    /// </summary>
    public string MessageBody { get; private set; } = string.Empty;

    /// <summary>
    /// When the notification should be sent (for reminders, this is a future time).
    /// </summary>
    public DateTimeOffset ScheduledAtUtc { get; private set; }

    public DateTimeOffset? SentAtUtc { get; private set; }

    public DateTimeOffset? FailedAtUtc { get; private set; }

    public int AttemptCount { get; private set; }

    public string? FailureReason { get; private set; }

    /// <summary>
    /// Correlation id linking to AppointmentEvent for end-to-end audit.
    /// </summary>
    public string? CorrelationId { get; private set; }

    private Notification() { } // EF Core

    public static Notification Create(
        Guid appointmentId,
        Guid patientId,
        NotificationType type,
        NotificationChannel channel,
        string phoneNumber,
        string messageBody,
        DateTimeOffset scheduledAtUtc,
        IClock clock,
        string? correlationId = null)
    {
        if (appointmentId == Guid.Empty) throw new ArgumentException("AppointmentId required.");
        if (patientId == Guid.Empty) throw new ArgumentException("PatientId required.");
        if (string.IsNullOrWhiteSpace(phoneNumber)) throw new ArgumentException("PhoneNumber required.");
        if (string.IsNullOrWhiteSpace(messageBody)) throw new ArgumentException("MessageBody required.");
        if (scheduledAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("ScheduledAtUtc must be UTC.");

        return new Notification
        {
            AppointmentId = appointmentId,
            PatientId = patientId,
            TypeValue = type.Value,
            ChannelValue = channel.Value,
            StatusValue = NotificationStatus.Pending.Value,
            PhoneNumber = phoneNumber,
            MessageBody = messageBody,
            ScheduledAtUtc = scheduledAtUtc,
            CorrelationId = correlationId,
            CreatedAtUtc = clock.UtcNow
        };
    }

    public void MarkSent(IClock clock)
    {
        StatusValue = NotificationStatus.Sent.Value;
        SentAtUtc ??= clock.UtcNow;
        FailureReason = null;
        AttemptCount++;
        Touch(clock.UtcNow);
    }

    public void MarkFailed(string reason, IClock clock)
    {
        StatusValue = NotificationStatus.Failed.Value;
        FailedAtUtc ??= clock.UtcNow;
        FailureReason = reason;
        AttemptCount++;
        Touch(clock.UtcNow);
    }

    public void MarkSkipped(IClock clock)
    {
        StatusValue = NotificationStatus.Skipped.Value;
        Touch(clock.UtcNow);
    }

    public bool IsDue(DateTimeOffset nowUtc) =>
        Status == NotificationStatus.Pending && ScheduledAtUtc <= nowUtc;

    public bool ShouldRetry(int maxAttempts) =>
        Status == NotificationStatus.Failed && AttemptCount < maxAttempts;
}
