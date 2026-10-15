namespace Dentara.Domain.Exceptions;

/// <summary>
/// Base class for all domain exceptions. Domain exceptions represent BUSINESS
/// RULE VIOLATIONS — not technical failures. They should be surfaced to the
/// user with a meaningful message.
///
/// Use domain exceptions (not ArgumentException) when the error is a business
/// invariant violation that the user can understand and act on, e.g.:
///   - "Cannot cancel a completed appointment"
///   - "Payment window has already expired"
///   - "Refund amount exceeds payment total"
///
/// Use ArgumentException for programming-contract violations (null args, bad format).
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>
    /// Machine-readable error code, e.g. "BOOKING_INVALID_TRANSITION".
    /// The API layer maps this to an HTTP status and a localized user message.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Optional: ID of the entity that the error relates to.
    /// </summary>
    public Guid? EntityId { get; }

    protected DomainException(string errorCode, string message, Guid? entityId = null)
        : base(message)
    {
        ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
        EntityId = entityId;
    }

    protected DomainException(string errorCode, string message, Exception innerException, Guid? entityId = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
        EntityId = entityId;
    }
}
