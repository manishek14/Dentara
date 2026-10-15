namespace Dentara.Domain.Events.Booking;

using Dentara.Domain.Common;

/// <summary>
/// Raised when an appointment transitions to Cancelled.
/// Side effects: SMS cancellation notice, trigger refund flow (if Confirmed).
/// </summary>
public sealed record AppointmentCancelledEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DentistId,
    string CancelledBy,
    string? CancellationReason,
    bool WasConfirmedPrior, // determines if refund flow should trigger
    decimal? RefundableAmount,
    string? Currency,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId = null)
    : DomainEvent(AppointmentId, OccurredAtUtc, CorrelationId);
