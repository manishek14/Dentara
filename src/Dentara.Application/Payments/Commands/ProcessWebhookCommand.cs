namespace Dentara.Application.Payments.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Payments;
using MediatR;

/// <summary>
/// Processes an incoming webhook from a payment gateway. Per ADR-006, this is
/// idempotent — re-processing the same ProviderEventId returns the original result.
/// Per ADR-004, this does NOT auto-confirm the appointment; it transitions
/// PaymentAttempt state and dispatches PaymentSucceededEvent which the
/// ConfirmAppointmentCommandHandler picks up.
/// </summary>
public sealed record ProcessWebhookCommand(
    PaymentProvider Provider,
    string ProviderEventId,
    string EventType,
    string RawPayload,
    string? SignatureHeader,
    string? CorrelationId = null)
    : ICommand<WebhookProcessedResult>, IHasCorrelationId, IIdempotentRequest
{
    public string Scope => "payment_callback";
    public string Key => $"{Provider.Name}|{ProviderEventId}";
    public TimeSpan? Ttl => null; // forever — webhook replay must always be safe
}

public sealed record WebhookProcessedResult(
    bool WasDuplicate,
    Guid? PaymentAttemptId,
    string? ProcessingNotes);
