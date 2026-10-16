namespace Dentara.Application.Booking.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Results;
using MediatR;

/// <summary>
/// Confirms an appointment after payment succeeds. Triggered by the
/// PaymentSucceededEvent handler in the Payments module. The command
/// transitions the appointment from PendingPayment → Confirmed.
///
/// NOTE: This command is NOT idempotent at the request level — the
/// idempotency comes from the PaymentSucceededEvent being deduplicated
/// upstream (via PaymentWebhookEvents UNIQUE constraint + IdempotencyRecord).
/// If the appointment is already Confirmed, the handler returns success
/// without re-raising events.
/// </summary>
public sealed record ConfirmAppointmentCommand(
    Guid AppointmentId,
    Guid PaymentAttemptId,
    decimal PaidAmount,
    string? CorrelationId = null)
    : ICommand<Unit>, IHasCorrelationId
{
}
