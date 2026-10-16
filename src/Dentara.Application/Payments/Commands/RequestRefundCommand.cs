namespace Dentara.Application.Payments.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Results;
using Dentara.Domain.Payments;
using MediatR;

/// <summary>
/// Requests a refund for a confirmed appointment. The RefundService (Domain)
/// handles the gateway call. Idempotent per ADR-006.
///
/// The refund amount is computed from the appointment's snapshot fields (ADR-007):
/// PriceAmountSnapshot × RefundablePercentSnapshot. The caller passes the
/// appointment so the handler can compute the amount.
/// </summary>
public sealed record RequestRefundCommand(
    Guid PaymentAttemptId,
    Guid AppointmentId,
    decimal RefundAmount,
    string Currency,
    string Reason,
    string? CorrelationId = null)
    : ICommand<RefundInitiatedResult>, IHasCorrelationId, IIdempotentRequest
{
    public string Scope => "refund";
    public string Key => $"{PaymentAttemptId}|{RefundAmount}|{Reason}";
    public TimeSpan? Ttl => TimeSpan.FromDays(7);
}

public sealed record RefundInitiatedResult(
    Guid RefundId,
    string Status);
