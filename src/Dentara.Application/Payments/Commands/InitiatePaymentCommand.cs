namespace Dentara.Application.Payments.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Common;
using Dentara.Domain.Payments;
using MediatR;

/// <summary>
/// Initiates a payment attempt for an appointment. Creates the PaymentAttempt
/// row, calls the gateway, and stores the authority + redirect URL. Returns
/// the redirect URL the client should navigate to.
/// </summary>
public sealed record InitiatePaymentCommand(
    Guid AppointmentId,
    Guid PatientId,
    decimal Amount,
    string Currency,
    string CallbackUrl,
    string? CorrelationId = null)
    : ICommand<PaymentInitiatedResult>, IHasCorrelationId, IIdempotentRequest
{
    public string Scope => "payment_initiate";
    public string Key => $"{AppointmentId}|{PatientId}|{Amount}|{Currency}";
    public TimeSpan? Ttl => TimeSpan.FromMinutes(30);
}

public sealed record PaymentInitiatedResult(
    Guid PaymentAttemptId,
    string? RedirectUrl);
