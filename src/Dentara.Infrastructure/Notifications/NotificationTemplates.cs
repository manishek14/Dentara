namespace Dentara.Infrastructure.Notifications;

using Dentara.Domain.Booking;
using Dentara.Domain.Notifications;

/// <summary>
/// Renders notification message bodies for different notification types.
/// All messages are in Persian (per the clinic's locale).
///
/// CRITICAL: messages must NOT include sensitive data like OTP codes,
/// payment card numbers, or full transaction details. They are appointment-
/// level reminders only.
/// </summary>
public static class NotificationTemplates
{
    public static string RenderAppointmentReminder(
        string dentistName,
        string serviceName,
        DateTimeOffset appointmentStartUtc,
        string persianDateTime)
    {
        return $"یادآوری نوبت: شما نوبت {serviceName} با {dentistName} " +
               $"را در تاریخ {persianDateTime} دارید. " +
               $"لطفاً ۱۵ دقیقه قبل از زمان نوبت در کلینیک حاضر باشید.";
    }

    public static string RenderAppointmentConfirmed(
        string dentistName,
        string serviceName,
        string persianDateTime)
    {
        return $"نوبت شما تأیید شد: {serviceName} با {dentistName} در تاریخ {persianDateTime}.";
    }

    public static string RenderAppointmentCancelled(
        string dentistName,
        string? reason,
        bool hasRefund,
        decimal? refundAmount)
    {
        var msg = $"نوبت شما با {dentistName} لغو شد.";
        if (!string.IsNullOrEmpty(reason))
            msg += $" دلیل: {reason}.";
        if (hasRefund && refundAmount.HasValue)
            msg += $" مبلغ {refundAmount.Value:N0} ریال بازپرداخت خواهد شد.";
        return msg;
    }

    public static string RenderAppointmentExpired()
    {
        return "مهلت پرداخت نوبت شما به پایان رسید. لطفاً نوبت جدید رزرو کنید.";
    }

    public static string RenderPaymentSucceeded(decimal amount, string currency)
    {
        return $"پرداخت شما به مبلغ {amount:N0} {currency} دریافت شد. نوبت شما تأیید شد.";
    }

    public static string RenderRefundCompleted(decimal amount, string currency)
    {
        return $"بازپرداخت به مبلغ {amount:N0} {currency} انجام شد.";
    }

    public static string RenderWaitlistOffered(
        string dentistName,
        string serviceName,
        string persianDateTime)
    {
        return $"نوبت خالی شد: {serviceName} با {dentistName} در تاریخ {persianDateTime}. " +
               $"برای رزرو، لطفاً به سامانه مراجعه کنید. این پیشنهاد تا ۳۰ دقیقه دیگر معتبر است.";
    }
}
