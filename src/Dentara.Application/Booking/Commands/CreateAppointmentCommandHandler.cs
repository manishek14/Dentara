namespace Dentara.Application.Booking.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Services.Booking;
using MediatR;
using System.Text.Json;

public sealed class CreateAppointmentCommandHandler
    : IRequestHandler<CreateAppointmentCommand, Result<AppointmentCreatedResult>>
{
    private readonly IBookingTransactionService _bookingTransactionService;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;

    public CreateAppointmentCommandHandler(
        IBookingTransactionService bookingTransactionService,
        IDomainEventDispatcher eventDispatcher,
        IAuditService auditService,
        ICurrentUserContext currentUser,
        IClock clock)
    {
        _bookingTransactionService = bookingTransactionService;
        _eventDispatcher = eventDispatcher;
        _auditService = auditService;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<AppointmentCreatedResult>> Handle(CreateAppointmentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var bookingRequest = new BookingRequest(
                request.PatientId,
                request.DentistId,
                request.ServiceId,
                request.PublishedSessionId,
                request.StartAtUtc,
                request.EndAtUtc,
                request.ServiceNameSnapshot,
                request.ServiceDurationMinutesSnapshot,
                request.PriceAmountSnapshot,
                request.CurrencySnapshot,
                request.DentistDisplayNameSnapshot,
                request.CancellationPolicyVersion,
                request.CancellationFeePercent,
                request.RefundablePercent,
                request.TermsSnapshotHash,
                request.RequiredResources.Select(r => new Domain.Services.Booking.ResourceRequirementItem(
                    r.ResourceTypeValue, r.QuantityRequired, r.RequireDentistOwned, r.SpecificResourceId)).ToList(),
                request.CorrelationId,
                request.PaymentWindowOverride);

            var appointment = await _bookingTransactionService.ReserveAppointmentAsync(bookingRequest, cancellationToken);

            // Dispatch domain events AFTER the transaction commits. Failures here
            // (e.g. SMS gateway down) must NOT roll back the booking.
            await _eventDispatcher.DispatchAsync(appointment, cancellationToken);

            await _auditService.LogAsync(new AuditEntry(
                EntityType: "Appointment",
                EntityId: appointment.Id,
                Action: "Create",
                PerformedBy: _currentUser.UserId.HasValue
                    ? $"User:{_currentUser.UserId}"
                    : "Anonymous",
                OccurredAtUtc: _clock.UtcNow,
                Description: $"Appointment created for dentist {request.DentistDisplayNameSnapshot}, service {request.ServiceNameSnapshot}",
                PerformedByUserId: _currentUser.UserId,
                NewValueJson: JsonSerializer.Serialize(new
                {
                    status = appointment.Status.Name,
                    patientId = appointment.PatientId,
                    dentistId = appointment.DentistId,
                    serviceId = appointment.ServiceId,
                    startAtUtc = appointment.StartAtUtc,
                    endAtUtc = appointment.EndAtUtc,
                    priceAmount = appointment.PriceAmountSnapshot,
                    currency = appointment.CurrencySnapshot,
                    expiresAtUtc = appointment.ExpiresAtUtc
                }),
                CorrelationId: request.CorrelationId), cancellationToken);

            return Result<AppointmentCreatedResult>.Success(new AppointmentCreatedResult(
                appointment.Id,
                appointment.ExpiresAtUtc,
                appointment.PriceAmountSnapshot,
                appointment.CurrencySnapshot));
        }
        catch (Exception ex)
        {
            return Result<AppointmentCreatedResult>.FromException(ex);
        }
    }
}
