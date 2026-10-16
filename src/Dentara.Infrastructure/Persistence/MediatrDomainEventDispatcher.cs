namespace Dentara.Infrastructure.Persistence;

using Dentara.Application.Common.Interfaces;
using Dentara.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

/// <summary>
/// Dispatches domain events from aggregates AFTER SaveChangesAsync commits.
/// Uses MediatR INotification so handlers are resolved via DI.
///
/// CRITICAL: this dispatch happens AFTER commit. If a handler fails (e.g. SMS
/// gateway down), the DB state is still committed. This is intentional per the
/// architecture — the system must not roll back a successful booking because
/// the SMS notification failed. Outbox pattern (future) handles reliable dispatch.
/// </summary>
public sealed class MediatrDomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MediatrDomainEventDispatcher> _logger;

    public MediatrDomainEventDispatcher(
        IServiceProvider serviceProvider,
        ILogger<MediatrDomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task DispatchAsync(AggregateRoot aggregate, CancellationToken cancellationToken = default)
    {
        if (aggregate == null) throw new ArgumentNullException(nameof(aggregate));

        // Snapshot the events before clearing them.
        var events = aggregate.DomainEvents.ToList();
        if (events.Count == 0) return;

        aggregate.ClearDomainEvents();

        foreach (var @event in events)
        {
            try
            {
                // Use MediatR's IMediator to publish — handlers are resolved via DI.
                // We publish via the IServiceProvider to avoid a hard MediatR reference
                // in this file (though MediatR IS in the Application layer).
                var mediator = _serviceProvider.GetRequiredService<MediatR.IMediator>();
                await mediator.Publish(@event, cancellationToken);

                _logger.LogDebug("Dispatched domain event {EventType} for aggregate {AggregateId}",
                    @event.GetType().Name, @event.AggregateId);
            }
            catch (Exception ex)
            {
                // Log and continue — do NOT fail the whole dispatch on one handler error.
                _logger.LogError(ex,
                    "Failed to dispatch domain event {EventType} for aggregate {AggregateId}: {Message}",
                    @event.GetType().Name, @event.AggregateId, ex.Message);
            }
        }
    }
}
