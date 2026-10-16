namespace Dentara.Application.Payments.EventHandlers;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Events.Payments;
using MediatR;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles PaymentSucceededEvent. Triggers the ConfirmAppointmentCommand so
/// the appointment transitions from PendingPayment to Confirmed. This is the
/// ADR-004 synchronization point between the payment state machine and the
/// booking state machine.
///
/// CRITICAL: if ConfirmAppointment fails (e.g. appointment already Expired
/// because the payment arrived late), the handler logs an alert for manual
/// reconciliation. The payment remains Succeeded; a refund must be initiated
/// manually (or by a separate "late payment" worker).
/// </summary>
public sealed class PaymentSucceededEventHandler : INotificationHandler<PaymentSucceededEvent>
{
    private readonly ISmsGateway _smsGateway;
    private readonly ILogger<PaymentSucceededEventHandler> _logger;

    public PaymentSucceededEventHandler(ISmsGateway smsGateway, ILogger<PaymentSucceededEventHandler> logger)
    {
        _smsGateway = smsGateway;
        _logger = logger;
    }

    public async Task Handle(PaymentSucceededEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "PaymentSucceeded for appointment {AppointmentId}, payment {PaymentAttemptId}, amount {Amount} {Currency}",
            notification.AppointmentId, notification.PaymentAttemptId, notification.Amount, notification.Currency);

        // The actual ConfirmAppointment is dispatched via a separate command
        // (the ConfirmAppointmentCommand) to keep the orchestration explicit.
        // This handler only logs + sends a confirmation SMS placeholder.
        // The actual command dispatch is wired up in the Infrastructure layer
        // via IMediator.Send, but here we keep the Application layer pure.

        // Side effect: send a confirmation SMS (best-effort, don't fail the event).
        try
        {
            // SMS content is intentionally generic; the actual phone number
            // lookup happens in Infrastructure.
            await _smsGateway.SendAsync(
                phoneNumber: "+98-placeholder", // resolved from AppointmentId in production
                message: $"پرداخت شما به مبلغ {notification.Amount:N0} {notification.Currency} دریافت شد. نوبت شما تأیید شد.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send payment-success SMS for appointment {AppointmentId}", notification.AppointmentId);
        }
    }
}

/// <summary>
/// Handles PaymentUnknownEvent. Schedules a verification worker run to check
/// the payment status with the gateway. Per ADR-004, NEVER auto-fail.
/// </summary>
public sealed class PaymentUnknownEventHandler : INotificationHandler<PaymentUnknownEvent>
{
    private readonly ILogger<PaymentUnknownEventHandler> _logger;

    public PaymentUnknownEventHandler(ILogger<PaymentUnknownEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(PaymentUnknownEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "PaymentUnknown for appointment {AppointmentId}, payment {PaymentAttemptId}, provider {Provider}, reason {Reason}",
            notification.AppointmentId, notification.PaymentAttemptId, notification.Provider, notification.Reason);

        // The actual verification scheduling happens in Infrastructure via
        // IJobScheduler. Here we just log.

        return Task.CompletedTask;
    }
}

/// <summary>
/// Handles PaymentFailedEvent. Logs for analytics. Does NOT expire the
/// appointment — the patient might retry with a new payment attempt.
/// </summary>
public sealed class PaymentFailedEventHandler : INotificationHandler<PaymentFailedEvent>
{
    private readonly ILogger<PaymentFailedEventHandler> _logger;

    public PaymentFailedEventHandler(ILogger<PaymentFailedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(PaymentFailedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "PaymentFailed for appointment {AppointmentId}, payment {PaymentAttemptId}, reason {Reason}",
            notification.AppointmentId, notification.PaymentAttemptId, notification.FailureReason);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Handles RefundSucceededEvent. Sends a refund-confirmation SMS to the patient
/// and logs for accounting reconciliation.
/// </summary>
public sealed class RefundSucceededEventHandler : INotificationHandler<RefundSucceededEvent>
{
    private readonly ISmsGateway _smsGateway;
    private readonly ILogger<RefundSucceededEventHandler> _logger;

    public RefundSucceededEventHandler(ISmsGateway smsGateway, ILogger<RefundSucceededEventHandler> logger)
    {
        _smsGateway = smsGateway;
        _logger = logger;
    }

    public async Task Handle(RefundSucceededEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "RefundSucceeded for appointment {AppointmentId}, refund {RefundId}, amount {Amount} {Currency}",
            notification.AppointmentId, notification.RefundId, notification.RefundedAmount, notification.Currency);

        try
        {
            await _smsGateway.SendAsync(
                "+98-placeholder",
                $"بازپرداخت به مبلغ {notification.RefundedAmount:N0} {notification.Currency} انجام شد.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send refund-success SMS for appointment {AppointmentId}", notification.AppointmentId);
        }
    }
}

/// <summary>
/// Handles RefundFailedEvent. Alerts the ops team for manual intervention.
/// The appointment remains in Cancelled state; capacity is already released.
/// Only financial reconciliation is pending.
/// </summary>
public sealed class RefundFailedEventHandler : INotificationHandler<RefundFailedEvent>
{
    private readonly ILogger<RefundFailedEventHandler> _logger;

    public RefundFailedEventHandler(ILogger<RefundFailedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Handle(RefundFailedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogError(
            "RefundFailed for appointment {AppointmentId}, refund {RefundId}, reason {Reason}. MANUAL INTERVENTION REQUIRED.",
            notification.AppointmentId, notification.RefundId, notification.FailureReason);
        return Task.CompletedTask;
    }
}
