namespace Dentara.Application.Common.Interfaces;

using Dentara.Domain.Common;
using Dentara.Domain.Events;

/// <summary>
/// Domain event dispatcher. Reads uncommitted events from aggregates after
/// SaveChangesAsync commits, dispatches them via MediatR INotification,
/// then clears the aggregate's event queue.
///
/// IMPORTANT: events are dispatched AFTER the DB transaction commits. If the
/// dispatch fails (e.g. SMS gateway down), the DB state is still committed.
/// This is intentional — the system must not roll back a successful booking
/// because the SMS notification failed. Outbox pattern (future) handles
/// reliable dispatch.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(AggregateRoot aggregate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Marker interface for domain event handlers. Concrete handlers implement this
/// with a specific IDomainEvent subtype. The dispatcher locates handlers via
/// DI and invokes them.
/// </summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
