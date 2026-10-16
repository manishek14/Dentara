namespace Dentara.Application.Payments.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Payments;
using Dentara.Domain.Services.Payments;
using MediatR;

public sealed class ProcessWebhookCommandHandler
    : IRequestHandler<ProcessWebhookCommand, Result<WebhookProcessedResult>>
{
    private readonly IWebhookProcessor _webhookProcessor;

    public ProcessWebhookCommandHandler(IWebhookProcessor webhookProcessor)
    {
        _webhookProcessor = webhookProcessor;
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

    public RequestRefundCommandHandler(IRefundService refundService)
    {
        _refundService = refundService;
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

            return Result<RefundInitiatedResult>.Success(new RefundInitiatedResult(
                refund.Id, refund.Status.Name));
        }
        catch (Exception ex)
        {
            return Result<RefundInitiatedResult>.FromException(ex);
        }
    }
}
