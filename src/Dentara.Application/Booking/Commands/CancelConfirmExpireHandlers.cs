namespace Dentara.Application.Booking.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Exceptions;
using Dentara.Domain.Services.Booking;
using MediatR;
using System.Text.Json;

public sealed class CancelAppointmentCommandHandler
    : IRequestHandler<CancelAppointmentCommand, Result>
{
    private readonly ICancellationService _cancellationService;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;

    public CancelAppointmentCommandHandler(
        ICancellationService cancellationService,
        IDomainEventDispatcher eventDispatcher,
        IAuditService auditService,
        ICurrentUserContext currentUser,
        IClock clock)
    {
        _cancellationService = cancellationService;
        _eventDispatcher = eventDispatcher;
        _auditService = auditService;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> Handle(CancelAppointmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var appointment = await _cancellationService.CancelAsync(
                request.AppointmentId,
                request.CancelledBy,
                request.Reason,
                request.CorrelationId,
                cancellationToken);

            await _eventDispatcher.DispatchAsync(appointment, cancellationToken);

            await _auditService.LogAsync(new AuditEntry(
                EntityType: "Appointment",
                EntityId: appointment.Id,
                Action: "Cancel",
                PerformedBy: request.CancelledBy,
                OccurredAtUtc: _clock.UtcNow,
                Description: $"Appointment cancelled. Reason: {request.Reason ?? "(none)"}",
                PerformedByUserId: _currentUser.UserId,
                NewValueJson: JsonSerializer.Serialize(new
                {
                    status = appointment.Status.Name,
                    cancelledBy = appointment.CancelledBy,
                    cancellationReason = appointment.CancellationReason,
                    refundAmount = appointment.CalculateRefundAmount()
                }),
                CorrelationId: request.CorrelationId), cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.FromException(ex);
        }
    }
}

public sealed class ConfirmAppointmentCommandHandler
    : IRequestHandler<ConfirmAppointmentCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Appointment> _appointmentRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;

    public ConfirmAppointmentCommandHandler(
        IUnitOfWork unitOfWork,
        IRepository<Domain.Booking.Appointment> appointmentRepository,
        IDomainEventDispatcher eventDispatcher,
        IAuditService auditService,
        ICurrentUserContext currentUser,
        IClock clock)
    {
        _unitOfWork = unitOfWork;
        _appointmentRepository = appointmentRepository;
        _eventDispatcher = eventDispatcher;
        _auditService = auditService;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> Handle(ConfirmAppointmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
            if (appointment is null)
                return Result.Failure("NOTFOUND_APPOINTMENT", $"Appointment {request.AppointmentId} not found.");

            // Idempotent: if already confirmed, return success without re-raising events.
            if (appointment.Status == Domain.Booking.AppointmentStatus.Confirmed)
                return Result.Success();

            appointment.Confirm(_clock, request.PaymentAttemptId, request.PaidAmount, request.CorrelationId);

            _appointmentRepository.Update(appointment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _eventDispatcher.DispatchAsync(appointment, cancellationToken);

            await _auditService.LogAsync(new AuditEntry(
                EntityType: "Appointment",
                EntityId: appointment.Id,
                Action: "Confirm",
                PerformedBy: _currentUser.UserId.HasValue
                    ? $"User:{_currentUser.UserId}"
                    : "System:PaymentCallback",
                OccurredAtUtc: _clock.UtcNow,
                Description: $"Appointment confirmed after payment {request.PaymentAttemptId}",
                PerformedByUserId: _currentUser.UserId,
                NewValueJson: JsonSerializer.Serialize(new
                {
                    status = appointment.Status.Name,
                    paymentAttemptId = request.PaymentAttemptId,
                    paidAmount = request.PaidAmount
                }),
                CorrelationId: request.CorrelationId), cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.FromException(ex);
        }
    }
}

public sealed class ExpireAppointmentCommandHandler
    : IRequestHandler<ExpireAppointmentCommand, Result>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Domain.Booking.Appointment> _appointmentRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IAuditService _auditService;
    private readonly IClock _clock;

    public ExpireAppointmentCommandHandler(
        IUnitOfWork unitOfWork,
        IRepository<Domain.Booking.Appointment> appointmentRepository,
        IDomainEventDispatcher eventDispatcher,
        IAuditService auditService,
        IClock clock)
    {
        _unitOfWork = unitOfWork;
        _appointmentRepository = appointmentRepository;
        _eventDispatcher = eventDispatcher;
        _auditService = auditService;
        _clock = clock;
    }

    public async Task<Result> Handle(ExpireAppointmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var appointment = await _appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
            if (appointment is null)
                return Result.Failure("NOTFOUND_APPOINTMENT", $"Appointment {request.AppointmentId} not found.");

            // Idempotent: if already terminal, return success.
            if (appointment.Status.IsTerminal)
                return Result.Success();

            appointment.Expire(_clock, request.PaymentWasVerifiedAbsent, request.CorrelationId);

            _appointmentRepository.Update(appointment);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _eventDispatcher.DispatchAsync(appointment, cancellationToken);

            await _auditService.LogAsync(new AuditEntry(
                EntityType: "Appointment",
                EntityId: appointment.Id,
                Action: "Expire",
                PerformedBy: "System:ExpiryWorker",
                OccurredAtUtc: _clock.UtcNow,
                Description: $"Appointment expired. Payment verified absent: {request.PaymentWasVerifiedAbsent}",
                NewValueJson: JsonSerializer.Serialize(new
                {
                    status = appointment.Status.Name,
                    paymentWasVerifiedAbsent = request.PaymentWasVerifiedAbsent
                }),
                CorrelationId: request.CorrelationId), cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.FromException(ex);
        }
    }
}
