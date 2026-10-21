namespace Dentara.Application.Notifications.EventHandlers;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Events.Booking;
using Dentara.Domain.Events.Payments;
using Dentara.Domain.Identity;
using Dentara.Domain.Notifications;
using Dentara.Infrastructure.Notifications;
using Dentara.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Handles AppointmentConfirmedEvent — creates an "appointment confirmed"
/// notification to be sent immediately.
/// </summary>
public sealed class NotificationEventHandlers :
    INotificationHandler<AppointmentConfirmedEvent>,
    INotificationHandler<AppointmentCancelledEvent>,
    INotificationHandler<AppointmentExpiredEvent>,
    INotificationHandler<PaymentSucceededEvent>,
    INotificationHandler<RefundSucceededEvent>
{
    private readonly DentaraDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILogger<NotificationEventHandlers> _logger;

    public NotificationEventHandlers(
        DentaraDbContext dbContext,
        IClock clock,
        ILogger<NotificationEventHandlers> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _logger = logger;
    }

    public async Task Handle(AppointmentConfirmedEvent notification, CancellationToken cancellationToken)
    {
        await CreateNotificationForAppointment(
            notification.AppointmentId,
            NotificationType.AppointmentConfirmed,
            notification.CorrelationId,
            cancellationToken);
    }

    public async Task Handle(AppointmentCancelledEvent notification, CancellationToken cancellationToken)
    {
        await CreateCancellationNotification(notification, cancellationToken);
    }

    public async Task Handle(AppointmentExpiredEvent notification, CancellationToken cancellationToken)
    {
        if (!notification.PaymentWasVerifiedAbsent)
            return; // Don't notify if we're not sure the payment failed

        await CreateNotificationForAppointment(
            notification.AppointmentId,
            NotificationType.AppointmentExpired,
            notification.CorrelationId,
            cancellationToken);
    }

    public async Task Handle(PaymentSucceededEvent notification, CancellationToken cancellationToken)
    {
        var message = NotificationTemplates.RenderPaymentSucceeded(notification.Amount, notification.Currency);
        await CreateNotification(
            notification.AppointmentId,
            notification.PatientId,
            NotificationType.PaymentSucceeded,
            message,
            notification.CorrelationId,
            cancellationToken);
    }

    public async Task Handle(RefundSucceededEvent notification, CancellationToken cancellationToken)
    {
        var message = NotificationTemplates.RenderRefundCompleted(notification.RefundedAmount, notification.Currency);
        await CreateNotification(
            notification.AppointmentId,
            notification.PatientId,
            NotificationType.RefundCompleted,
            message,
            notification.CorrelationId,
            cancellationToken);
    }

    private async Task CreateNotificationForAppointment(
        Guid appointmentId,
        NotificationType type,
        string? correlationId,
        CancellationToken ct)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(a => a.Id == appointmentId, ct);
        if (appointment == null) return;

        var patient = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == appointment.PatientId, ct);
        if (patient == null) return;

        string message = type switch
        {
            _ when type == NotificationType.AppointmentConfirmed =>
                NotificationTemplates.RenderAppointmentConfirmed(
                    appointment.DentistDisplayNameSnapshot,
                    appointment.ServiceNameSnapshot,
                    $"{appointment.StartAtUtc:yyyy/MM/dd HH:mm} UTC"),
            _ when type == NotificationType.AppointmentExpired =>
                NotificationTemplates.RenderAppointmentExpired(),
            _ => $"اطلاع‌رسانی نوبت: {type.Name}"
        };

        await CreateNotification(appointmentId, appointment.PatientId, type, message, correlationId, ct);
    }

    private async Task CreateCancellationNotification(
        AppointmentCancelledEvent evt,
        CancellationToken ct)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(a => a.Id == evt.AppointmentId, ct);
        if (appointment == null) return;

        var patient = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == evt.PatientId, ct);
        if (patient == null) return;

        var message = NotificationTemplates.RenderAppointmentCancelled(
            appointment.DentistDisplayNameSnapshot,
            evt.CancellationReason,
            evt.WasConfirmedPrior,
            evt.RefundableAmount);

        await CreateNotification(evt.AppointmentId, evt.PatientId,
            NotificationType.AppointmentCancelled, message, evt.CorrelationId, ct);
    }

    private async Task CreateNotification(
        Guid appointmentId,
        Guid patientId,
        NotificationType type,
        string message,
        string? correlationId,
        CancellationToken ct)
    {
        var patient = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == patientId, ct);
        if (patient == null) return;

        // Immediate delivery (scheduled for now)
        var notification = Notification.Create(
            appointmentId,
            patientId,
            type,
            NotificationChannel.Sms,
            patient.PhoneNumber,
            message,
            _clock.UtcNow,
            _clock,
            correlationId);

        await _dbContext.Notifications.AddAsync(notification, ct);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created {Type} notification {NotificationId} for appointment {AppointmentId}",
            type.Name, notification.Id, appointmentId);
    }
}
