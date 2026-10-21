namespace Dentara.Domain.Notifications;

using Dentara.Domain.Common;

/// <summary>
/// Type of notification — drives the message template and delivery channel.
/// </summary>
public sealed class NotificationType : Enumeration
{
    public static readonly NotificationType AppointmentReminder = new(1, nameof(AppointmentReminder));
    public static readonly NotificationType AppointmentConfirmed = new(2, nameof(AppointmentConfirmed));
    public static readonly NotificationType AppointmentCancelled = new(3, nameof(AppointmentCancelled));
    public static readonly NotificationType AppointmentRescheduled = new(4, nameof(AppointmentRescheduled));
    public static readonly NotificationType AppointmentExpired = new(5, nameof(AppointmentExpired));
    public static readonly NotificationType PaymentSucceeded = new(6, nameof(PaymentSucceeded));
    public static readonly NotificationType RefundCompleted = new(7, nameof(RefundCompleted));
    public static readonly NotificationType WaitlistOffered = new(8, nameof(WaitlistOffered));

    private NotificationType(int value, string name) : base(value, name) { }

    public static IEnumerable<NotificationType> All() => GetAll<NotificationType>();
    public static NotificationType FromValue(int value) => FromValue<NotificationType>(value);
    public static NotificationType FromName(string name) => FromName<NotificationType>(name);
}

/// <summary>
/// Delivery status of a notification.
/// </summary>
public sealed class NotificationStatus : Enumeration
{
    public static readonly NotificationStatus Pending = new(1, nameof(Pending));
    public static readonly NotificationStatus Sent = new(2, nameof(Sent));
    public static readonly NotificationStatus Failed = new(3, nameof(Failed));
    public static readonly NotificationStatus Skipped = new(4, nameof(Skipped)); // e.g. patient opted out

    private NotificationStatus(int value, string name) : base(value, name) { }

    public static IEnumerable<NotificationStatus> All() => GetAll<NotificationStatus>();
    public static NotificationStatus FromValue(int value) => FromValue<NotificationStatus>(value);
    public static NotificationStatus FromName(string name) => FromName<NotificationStatus>(name);
}

/// <summary>
/// Delivery channel for a notification.
/// </summary>
public sealed class NotificationChannel : Enumeration
{
    public static readonly NotificationChannel Sms = new(1, nameof(Sms));
    public static readonly NotificationChannel Email = new(2, nameof(Email));
    public static readonly NotificationChannel Push = new(3, nameof(Push));

    private NotificationChannel(int value, string name) : base(value, name) { }

    public static IEnumerable<NotificationChannel> All() => GetAll<NotificationChannel>();
    public static NotificationChannel FromValue(int value) => FromValue<NotificationChannel>(value);
    public static NotificationChannel FromName(string name) => FromName<NotificationChannel>(name);
}
