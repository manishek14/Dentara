namespace Dentara.Domain.Exceptions.Payments;

using Dentara.Domain.Exceptions;

/// <summary>
/// Raised when a duplicate idempotency key is detected. The operation has
/// already been processed (or is in progress). The caller should either return
/// the cached response or wait/retry depending on the in-progress record's age.
/// </summary>
public sealed class IdempotencyConflictException : DomainException
{
    public string Scope { get; }
    public string Key { get; }
    public string ExistingStatus { get; }

    public IdempotencyConflictException(string scope, string key, string existingStatus)
        : base(
            "IDEMPOTENCY_CONFLICT",
            $"Operation '{scope}/{key}' has already been processed (status: {existingStatus}).")
    {
        Scope = scope;
        Key = key;
        ExistingStatus = existingStatus;
    }
}
