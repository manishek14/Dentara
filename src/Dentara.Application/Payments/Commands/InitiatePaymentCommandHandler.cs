namespace Dentara.Application.Payments.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Common;
using Dentara.Domain.Exceptions;
using Dentara.Domain.Payments;
using MediatR;
using System.Text.Json;

public sealed class InitiatePaymentCommandHandler
    : IRequestHandler<InitiatePaymentCommand, Result<PaymentInitiatedResult>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<PaymentAttempt> _paymentRepository;
    private readonly IPaymentGateway _gateway;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;

    public InitiatePaymentCommandHandler(
        IUnitOfWork unitOfWork,
        IRepository<PaymentAttempt> paymentRepository,
        IPaymentGateway gateway,
        IAuditService auditService,
        ICurrentUserContext currentUser,
        IClock clock)
    {
        _unitOfWork = unitOfWork;
        _paymentRepository = paymentRepository;
        _gateway = gateway;
        _auditService = auditService;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<PaymentInitiatedResult>> Handle(InitiatePaymentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var attempt = PaymentAttempt.Create(
                request.AppointmentId,
                request.PatientId,
                _gateway.Provider,
                request.Amount,
                request.Currency,
                _clock);

            await _paymentRepository.AddAsync(attempt, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Gateway call AFTER the first save — keeps the attempt row even if
            // the gateway is slow. The transaction is NOT held open during the call.
            var gatewayResponse = await _gateway.InitiateAsync(new GatewayInitiationRequest(
                attempt.Id, request.Amount, request.Currency, request.CallbackUrl,
                $"Dentara appointment {request.AppointmentId}"), cancellationToken);

            if (!gatewayResponse.Success || string.IsNullOrEmpty(gatewayResponse.Authority))
                return Result<PaymentInitiatedResult>.Failure(
                    "PAYMENT_GATEWAY_INITIATION_FAILED",
                    gatewayResponse.FailureReason ?? "Gateway did not return an authority.");

            attempt.MarkPending(gatewayResponse.Authority, gatewayResponse.RedirectUrl, _clock);
            _paymentRepository.Update(attempt);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _auditService.LogAsync(new AuditEntry(
                EntityType: "PaymentAttempt",
                EntityId: attempt.Id,
                Action: "Initiate",
                PerformedBy: _currentUser.UserId.HasValue
                    ? $"User:{_currentUser.UserId}"
                    : "Anonymous",
                OccurredAtUtc: _clock.UtcNow,
                Description: $"Payment initiated for appointment {request.AppointmentId}, amount {request.Amount} {request.Currency}",
                PerformedByUserId: _currentUser.UserId,
                NewValueJson: JsonSerializer.Serialize(new
                {
                    appointmentId = attempt.AppointmentId,
                    provider = attempt.Provider.Name,
                    amount = attempt.Amount,
                    currency = attempt.Currency,
                    status = attempt.Status.Name
                }),
                CorrelationId: request.CorrelationId), cancellationToken);

            return Result<PaymentInitiatedResult>.Success(new PaymentInitiatedResult(
                attempt.Id, gatewayResponse.RedirectUrl));
        }
        catch (Exception ex)
        {
            return Result<PaymentInitiatedResult>.FromException(ex);
        }
    }
}
