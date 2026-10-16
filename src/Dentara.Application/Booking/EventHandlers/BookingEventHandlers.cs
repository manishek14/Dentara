namespace Dentara.Application.Booking.EventHandlers;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Events.Booking;
using MediatR;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles AppointmentCreatedEvent. Sends a "your booking is pending payment"
/// SMS to the patient with the 10-minute payment window reminder.
/// </summary>
public sealed class AppointmentCreatedEventHandler : INotificationHandler<AppointmentCreatedEvent>
{
    private readonly ISmsGateway _smsGateway;
    private readonly ILogger<AppointmentCreatedEventHandler> _logger;

    public AppointmentCreatedEventHandler(ISmsGateway smsGateway, ILogger<AppointmentCreatedEventHandler> logger)
    {
        _smsGateway = smsGateway;
        _logger = logger;
    }

    public async Task Handle(AppointmentCreatedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AppointmentCreated {AppointmentId} for patient {PatientId}, expires at {ExpiresAtUtc}",
            notification.AppointmentId, notification.PatientId, notification.ExpiresAtUtc);

        try
        {
            await _smsGateway.SendAsync(
                "+98-placeholder",
                $"نوبت شما ثبت شد. لطفاً تا {notification.ExpiresAtUtc:HH:mm} پرداخت کنید.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send appointment-created SMS for {AppointmentId}", notification.AppointmentId);
        }
    }
}

/// <summary>
/// Handles AppointmentExpiredEvent. Sends an "your booking expired" SMS.
/// </summary>
public sealed class AppointmentExpiredEventHandler : INotificationHandler<AppointmentExpiredEvent>
{
    private readonly ISmsGateway _smsGateway;
    private readonly ILogger<AppointmentExpiredEventHandler> _logger;

    public AppointmentExpiredEventHandler(ISmsGateway smsGateway, ILogger<AppointmentExpiredEventHandler> logger)
    {
        _smsGateway = smsGateway;
        _logger = logger;
    }

    public async Task Handle(AppointmentExpiredEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AppointmentExpired {AppointmentId}, paymentWasVerifiedAbsent={Verified}",
            notification.AppointmentId, notification.PaymentWasVerifiedAbsent);

        if (!notification.PaymentWasVerifiedAbsent)
        {
            _logger.LogWarning(
                "AppointmentExpired event raised without payment verification for {AppointmentId} — this is an ADR-004 violation",
                notification.AppointmentId);
            return;
        }

        try
        {
            await _smsGateway.SendAsync(
                "+98-placeholder",
                "مهلت پرداخت نوبت شما به پایان رسید. لطفاً نوبت جدید رزرو کنید.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send appointment-expired SMS for {AppointmentId}", notification.AppointmentId);
        }
    }
}

/// <summary>
/// Handles AppointmentCancelledEvent. Sends a cancellation-confirmation SMS.
/// If the cancellation came from a Confirmed appointment, the refund flow is
/// triggered separately by the cancellation service.
/// </summary>
public sealed class AppointmentCancelledEventHandler : INotificationHandler<AppointmentCancelledEvent>
{
    private readonly ISmsGateway _smsGateway;
    private readonly ILogger<AppointmentCancelledEventHandler> _logger;

    public AppointmentCancelledEventHandler(ISmsGateway smsGateway, ILogger<AppointmentCancelledEventHandler> logger)
    {
        _smsGateway = smsGateway;
        _logger = logger;
    }

    public async Task Handle(AppointmentCancelledEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AppointmentCancelled {AppointmentId} by {CancelledBy}, wasConfirmed={WasConfirmed}, refundable={Refundable}",
            notification.AppointmentId, notification.CancelledBy, notification.WasConfirmedPrior, notification.RefundableAmount);

        var message = notification.WasConfirmedPrior && notification.RefundableAmount.HasValue
            ? $"نوبت شما لغو شد. مبلغ {notification.RefundableAmount.Value:N0} {notification.Currency} بازپرداخت خواهد شد."
            : "نوبت شما لغو شد.";

        try
        {
            await _smsGateway.SendAsync("+98-placeholder", message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send appointment-cancelled SMS for {AppointmentId}", notification.AppointmentId);
        }
    }
}
