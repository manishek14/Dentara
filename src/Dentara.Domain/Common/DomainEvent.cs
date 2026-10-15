namespace Dentara.Domain.Common;

/// <summary>
/// Base class for domain events with common fields. Concrete event classes
/// inherit from this and add event-specific data.
/// </summary>
public abstract record DomainEvent : IDomainEvent
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
