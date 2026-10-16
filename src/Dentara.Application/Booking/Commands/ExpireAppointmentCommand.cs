namespace Dentara.Application.Booking.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Results;
using MediatR;

/// <summary>
/// Marks an appointment as expired. Called by the expiry worker AFTER it
/// verifies with the gateway that no payment is in Unknown/Succeeded state
/// (ADR-004 critical scenario).
///
/// If the appointment is already Expired/Cancelled, returns success (idempotent).
/// </summary>
public sealed record ExpireAppointmentCommand(
    Guid AppointmentId,
    bool PaymentWasVerifiedAbsent,
    string? CorrelationId = null)
    : ICommand<Unit>, IHasCorrelationId
{
}
