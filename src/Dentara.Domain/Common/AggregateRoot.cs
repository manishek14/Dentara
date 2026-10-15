namespace Dentara.Domain.Common;

/// <summary>
/// Base class for aggregate roots. An aggregate root is the only entry point
/// through which external code can modify the aggregate's state. Inner entities
/// (like ResourceAllocation inside Appointment) are only accessible through the root.
///
/// This base class provides:
///   - Domain event collection (raised by aggregate methods, dispatched after commit)
///   - Uncommitted event access for the Application layer
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _events = new();

    /// <summary>
    /// Events raised by this aggregate since it was loaded/created. The Application
    /// layer reads these after SaveChanges to dispatch them (typically via MediatR
    /// INotification) to handlers (SMS, email, audit, etc.).
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events.AsReadOnly();

    /// <summary>
    /// Raise a domain event. Called by aggregate methods when a business-significant
    /// side effect should occur (e.g. AppointmentConfirmed → SendNotification).
    /// </summary>
    protected void RaiseEvent(IDomainEvent @event)
    {
        if (@event == null) throw new ArgumentNullException(nameof(@event));
        _events.Add(@event);
    }

    /// <summary>
    /// Clear all uncommitted events. Called by the Application layer after
    /// events have been dispatched.
    /// </summary>
    public void ClearDomainEvents() => _events.Clear();
}
