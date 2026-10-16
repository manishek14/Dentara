namespace Dentara.Application.Common.Behaviors;

using Dentara.Application.Common.Interfaces;
using Dentara.Application.Common.Results;
using MediatR;
using Microsoft.Extensions.Logging;

/// <summary>
/// MediatR pipeline behavior for idempotency. Wraps requests that implement
/// IIdempotentRequest. Per ADR-006: if a completed IdempotencyRecord exists
/// for the (scope, key), the stored response is replayed. If an in-progress
/// record exists, throws IdempotencyConflictException.
///
/// The record is created BEFORE the handler runs (Status=InProgress), and
/// updated AFTER (Status=Completed or Failed). This ensures that even if the
/// handler crashes, the next retry will see an in-progress record and either
/// wait or skip — never double-execute.
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IIdempotencyService _idempotencyService;
    private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;

    public IdempotencyBehavior(
        IIdempotencyService idempotencyService,
        ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
    {
        _idempotencyService = idempotencyService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (request is not IIdempotentRequest idempotentRequest)
            return await next(cancellationToken);

        // Wrap TResponse in Result<TResponse> for storage. The idempotency
        // service handles the actual IdempotencyRecord lifecycle.
        var wrapped = await _idempotencyService.ExecuteAsync(
            idempotentRequest.Scope,
            idempotentRequest.Key,
            async ct => Result<TResponse>.Success(await next(ct)),
            cancellationToken,
            idempotentRequest.Ttl);

        return wrapped.IsSuccess && wrapped.Value != null
            ? wrapped.Value
            : default!;
    }
}

/// <summary>
/// Marker interface for requests that should be wrapped in idempotency logic.
/// </summary>
public interface IIdempotentRequest
{
    /// <summary>
    /// Idempotency scope, e.g. "payment_callback", "refund", "appointment_cancel".
    /// </summary>
    string Scope { get; }

    /// <summary>
    /// Unique key within the scope.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Optional TTL for the idempotency record. Null means forever (until cleanup).
    /// </summary>
    TimeSpan? Ttl { get; }
}
