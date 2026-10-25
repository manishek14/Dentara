namespace Dentara.Application.Payments.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Common;
using Dentara.Domain.Payments;
using Dentara.Domain.Services.Payments;
using MediatR;
using System.Text.Json;

public sealed class ProcessWebhookCommandHandler
    : IRequestHandler<ProcessWebhookCommand, Result<WebhookProcessedResult>>
{
    private readonly IWebhookProcessor _webhookProcessor;
    private readonly IAuditService _auditService;
    private readonly IClock _clock;

    public ProcessWebhookCommandHandler(
        IWebhookProcessor webhookProcessor,
        IAuditService auditService,
        IClock clock)
    {
        _webhookProcessor = webhookProcessor;
        _auditService = auditService;
        _clock = clock;
    }

    public async Task<Result<WebhookProcessedResult>> Handle(ProcessWebhookCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var webhookRequest = new WebhookRequest(
                request.Provider,
                request.ProviderEventId,
                request.EventType,
                request.RawPayload,
                request.SignatureHeader,
                request.CorrelationId);

            var result = await _webhookProcessor.ProcessAsync(webhookRequest, cancellationToken);

            // Audit log — only for non-duplicate webhooks (duplicates are no-ops)
            if (!result.WasDuplicate)
            {
                await _auditService.LogAsync(new AuditEntry(
                    EntityType: "PaymentWebhook",
                    EntityId: result.PaymentAttemptId ?? Guid.Empty,
                    Action: "WebhookProcessed",
                    PerformedBy: $"Gateway:{request.Provider.Name}",
                    OccurredAtUtc: _clock.UtcNow,
                    Description: $"Webhook {request.EventType} from {request.Provider.Name}, event {request.ProviderEventId}",
                    NewValueJson: JsonSerializer.Serialize(new
                    {
                        provider = request.Provider.Name,
                        eventId = request.ProviderEventId,
                        eventType = request.EventType,
                        paymentAttemptId = result.PaymentAttemptId,
                        processingNotes = result.ProcessingNotes
                    }),
                    CorrelationId: request.CorrelationId), cancellationToken);
            }

            return Result<WebhookProcessedResult>.Success(new WebhookProcessedResult(
                result.WasDuplicate,
                result.PaymentAttemptId,
                result.ProcessingNotes));
        }
        catch (Exception ex)
        {
            return Result<WebhookProcessedResult>.FromException(ex);
        }
    }
}

public sealed class RequestRefundCommandHandler
    : IRequestHandler<RequestRefundCommand, Result<RefundInitiatedResult>>
{
    private readonly IRefundService _refundService;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IClock _clock;

    public RequestRefundCommandHandler(
        IRefundService refundService,
        IAuditService auditService,
        ICurrentUserContext currentUser,
        IClock clock)
    {
        _refundService = refundService;
        _auditService = auditService;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<RefundInitiatedResult>> Handle(RequestRefundCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var refund = await _refundService.InitiateRefundAsync(
                request.PaymentAttemptId,
                request.RefundAmount,
                request.Reason,
                request.CorrelationId,
                cancellationToken);

            await _auditService.LogAsync(new AuditEntry(
                EntityType: "Refund",
                EntityId: refund.Id,
                Action: "RequestRefund",
                PerformedBy: _currentUser.UserId.HasValue
                    ? $"User:{_currentUser.UserId}"
                    : "System",
                OccurredAtUtc: _clock.UtcNow,
                Description: $"Refund requested for payment {request.PaymentAttemptId}, amount {request.RefundAmount} {request.Currency}. Reason: {request.Reason}",
                PerformedByUserId: _currentUser.UserId,
                NewValueJson: JsonSerializer.Serialize(new
                {
                    refundId = refund.Id,
                    paymentAttemptId = refund.PaymentAttemptId,
                    appointmentId = refund.AppointmentId,
                    amount = refund.Amount,
                    currency = refund.Currency,
                    status = refund.Status.Name,
                    reason = refund.Reason
                }),
                CorrelationId: request.CorrelationId), cancellationToken);

            return Result<RefundInitiatedResult>.Success(new RefundInitiatedResult(
                refund.Id, refund.Status.Name));
        }
        catch (Exception ex)
        {
            return Result<RefundInitiatedResult>.FromException(ex);
        }
    }
}
