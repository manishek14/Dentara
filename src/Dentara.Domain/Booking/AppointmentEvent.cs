namespace Dentara.Domain.Booking;

using Dentara.Domain.Common;

/// <summary>
/// Immutable audit-trail row recording every state transition and significant
/// event on an Appointment. Per ADR-004, this provides the basis for dispute
/// resolution and financial audit.
///
/// Events are append-only. They are never updated or deleted.
/// </summary>
public sealed class AppointmentEvent : Entity
{
    public Guid AppointmentId { get; private set; }

    public int EventTypeValue { get; private set; }

    public AppointmentEventType EventType => AppointmentEventType.FromValue(EventTypeValue);

    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary>
    /// Free-text description of the event, including relevant context.
    /// Examples: "Payment succeeded via Zarinpal, tracking id xyz", "Cancelled by patient".
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Optional JSON payload with structured event data (e.g. payment id, refund id).
    /// </summary>
    public string? PayloadJson { get; private set; }

    /// <summary>
    /// Correlation id linking this event to upstream operations (e.g. payment
    /// provider's tracking id, idempotency key). Useful for end-to-end tracing.
    /// </summary>
    public string? CorrelationId { get; private set; }

    private AppointmentEvent() { } // EF Core

    internal static AppointmentEvent Create(
        Guid appointmentId,
        AppointmentEventType eventType,
        IClock clock,
        string? notes = null,
        string? payloadJson = null,
        string? correlationId = null)
    {
        if (appointmentId == Guid.Empty)
            throw new ArgumentException("AppointmentId required.", nameof(appointmentId));

        return new AppointmentEvent
        {
            AppointmentId = appointmentId,
            EventTypeValue = eventType.Value,
            OccurredAtUtc = clock.UtcNow,
            Notes = notes,
            PayloadJson = payloadJson,
            CorrelationId = correlationId,
            CreatedAtUtc = clock.UtcNow
        };
    }

    /// <summary>
    /// Public factory for events raised by external services (e.g. payment service
    /// recording a refund completion). Used by Application layer.
    /// </summary>
    public static AppointmentEvent Raise(
        Guid appointmentId,
        AppointmentEventType eventType,
        IClock clock,
        string? notes = null,
        string? payloadJson = null,
        string? correlationId = null)
        => Create(appointmentId, eventType, clock, notes, payloadJson, correlationId);
}
