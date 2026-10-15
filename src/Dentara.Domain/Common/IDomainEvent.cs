namespace Dentara.Domain.Common;

/// <summary>
/// Marker interface for domain events. Raised by aggregates, dispatched by the
/// Application layer (typically via MediatR) AFTER the DB transaction commits.
///
/// Domain events capture SIDE EFFECTS of business operations:
///   - AppointmentConfirmed → SendSmsNotification
///   - AppointmentCancelled → IssueRefund
///   - PaymentSucceeded → ConfirmAppointment
///
/// They are NOT used for state transitions within the aggregate. All invariants
/// and state changes must be enforced synchronously inside the aggregate method.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// When the event was raised, in UTC.
    /// </summary>
    DateTimeOffset OccurredAtUtc { get; }

    /// <summary>
    /// The aggregate instance that raised this event.
    /// </summary>
    Guid AggregateId { get; }

    /// <summary>
    /// Optional correlation id for end-to-end tracing.
    /// </summary>
    string? CorrelationId { get; }
}
