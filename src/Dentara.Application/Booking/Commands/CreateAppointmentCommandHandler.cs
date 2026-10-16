namespace Dentara.Application.Booking.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Booking;
using Dentara.Domain.Common;
using Dentara.Domain.Services.Booking;
using MediatR;

public sealed class CreateAppointmentCommandHandler
    : IRequestHandler<CreateAppointmentCommand, Result<AppointmentCreatedResult>>
{
    private readonly IBookingTransactionService _bookingTransactionService;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly IClock _clock;

    public CreateAppointmentCommandHandler(
        IBookingTransactionService bookingTransactionService,
        IDomainEventDispatcher eventDispatcher,
        IClock clock)
    {
        _bookingTransactionService = bookingTransactionService;
        _eventDispatcher = eventDispatcher;
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
