namespace Dentara.Application.Common.Interfaces;

using Dentara.Application.Common.Results;

/// <summary>
/// Idempotency service for Application layer operations. Per ADR-006, every
/// sensitive operation (callback, refund, cancellation, OTP) carries an
/// idempotency key. This service wraps the operation with INSERT-or-retrieve
/// logic against IdempotencyRecords.
/// </summary>
public interface IIdempotencyService
{
    /// <summary>
    /// Executes the operation with idempotency. If a previous completed record
    /// exists, returns its stored response. If a previous in-progress record
    /// exists, throws IdempotencyConflictException. Otherwise executes the
    /// operation and stores the response.
    /// </summary>
    Task<Result<TResponse>> ExecuteAsync<TResponse>(
        string scope,
        string key,
        Func<CancellationToken, Task<Result<TResponse>>> operation,
        CancellationToken cancellationToken = default,
        TimeSpan? ttl = null);
}
