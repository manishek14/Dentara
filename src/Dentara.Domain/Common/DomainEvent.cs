namespace Dentara.Domain.Common;

using MediatR;

/// <summary>
/// Base class for domain events with common fields. Inherits from MediatR.INotification
/// so the Application layer can dispatch domain events via the same MediatR pipeline
/// used for commands/queries. This pulls a MediatR reference into the Domain layer;
/// it is acceptable because INotification is a pure marker interface with no behavior.
///
/// Concrete event classes inherit from this and add event-specific data.
/// </summary>
public abstract record DomainEvent : IDomainEvent, INotification
{
    public DateTimeOffset OccurredAtUtc { get; init; }

    public Guid AggregateId { get; init; }

    public string? CorrelationId { get; init; }

    protected DomainEvent(Guid aggregateId, DateTimeOffset occurredAtUtc, string? correlationId = null)
    {
        if (aggregateId == Guid.Empty)
            throw new ArgumentException("AggregateId is required.", nameof(aggregateId));
        if (occurredAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("OccurredAtUtc must be UTC.", nameof(occurredAtUtc));

        AggregateId = aggregateId;
        OccurredAtUtc = occurredAtUtc;
        CorrelationId = correlationId;
    }
}
