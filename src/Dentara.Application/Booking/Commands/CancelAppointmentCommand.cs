namespace Dentara.Application.Booking.Commands;

using Dentara.Application.Common;
using Dentara.Application.Common.Behaviors;
using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using Dentara.Domain.Services.Booking;
using MediatR;

/// <summary>
/// Cancels an existing appointment. Atomically releases capacity and creates
/// a Refund row if the appointment was Confirmed. Idempotent.
/// </summary>
public sealed record CancelAppointmentCommand(
    Guid AppointmentId,
    string CancelledBy,
    string? Reason = null,
    string? CorrelationId = null)
    : ICommand<Unit>, IHasCorrelationId, IIdempotentRequest
{
    public string Scope => "appointment_cancel";
    public string Key => $"{AppointmentId}|cancel";
    public TimeSpan? Ttl => null; // forever
}
