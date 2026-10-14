namespace Dentara.Domain.Payments;

using Dentara.Domain.Common;

/// <summary>
/// Idempotency record per ADR-006. Each external or sensitive operation
/// (callback processing, refund, cancellation, SMS OTP, worker job) carries
/// an Idempotency Key. The (Scope, Key) pair is UNIQUE in the database.
///
/// Lifecycle:
///   1. Before the operation: INSERT a row with Status=InProgress.
///      If INSERT fails (duplicate), the operation has already been (or is being)
///      handled — return the stored response or 409 Conflict.
///   2. Execute the operation.
///   3. UPDATE the row to Completed (with ResponsePayload) or Failed.
///
/// Cleanup: a monthly job removes records older than 90 days.
/// </summary>
public sealed class IdempotencyRecord : Entity
{
    /// <summary>
    /// Logical scope, e.g. "payment_callback", "refund", "appointment_cancel",
    /// "sms_otp", "worker_job". Free-text but enforced by convention.
    /// </summary>
    public string Scope { get; private set; } = string.Empty;

    /// <summary>
    /// Unique key within the scope. Examples:
    ///   - For payment callback: "{Provider}|{ProviderEventId}"
    ///   - For refund: "{PaymentAttemptId}|refund"
    ///   - For appointment cancel: "{AppointmentId}|cancel"
    ///   - For SMS OTP: "{PhoneNumber}|{Purpose}"
    /// </summary>
    public string Key { get; private set; } = string.Empty;

    public Guid? UserId { get; private set; }

    /// <summary>
    /// SHA-256 hash of the request payload. If the same key arrives with a
    /// different hash, the operator must investigate (likely a bug).
    /// </summary>
    public string? RequestHash { get; private set; }

    public int StatusValue { get; private set; }

    public IdempotencyStatus Status => IdempotencyStatus.FromValue(StatusValue);

    /// <summary>
    /// JSON-serialized response payload. Returned to clients on retry.
    /// </summary>
    public string? ResponsePayload { get; private set; }

    /// <summary>
    /// Optional HTTP status code to return on retry.
    /// </summary>
    public int? ResponseStatusCode { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    /// <summary>
    /// Optional expiry. Records older than this can be cleaned up.
    /// </summary>
    public DateTimeOffset? ExpiresAtUtc { get; private set; }

    /// <summary>
    /// Correlation id linking to AppointmentEvent for end-to-end audit.
    /// </summary>
    public string? CorrelationId { get; private set; }

    private IdempotencyRecord() { } // EF Core

    /// <summary>
    /// Returns the natural key used for the UNIQUE constraint.
    /// </summary>
    public string NaturalKey => $"{Scope}|{Key}";

    public static IdempotencyRecord Create(
        string scope,
        string key,
        IClock clock,
        Guid? userId = null,
        string? requestHash = null,
        TimeSpan? ttl = null,
        string? correlationId = null)
    {
        if (string.IsNullOrWhiteSpace(scope))
            throw new ArgumentException("Scope is required.", nameof(scope));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));
        if (scope.Length > 64)
            throw new ArgumentException("Scope must be at most 64 chars.", nameof(scope));
        if (key.Length > 256)
            throw new ArgumentException("Key must be at most 256 chars.", nameof(key));

        return new IdempotencyRecord
        {
            Scope = scope.Trim(),
            Key = key.Trim(),
            UserId = userId,
            RequestHash = requestHash,
            StatusValue = IdempotencyStatus.InProgress.Value,
            ExpiresAtUtc = ttl.HasValue ? clock.UtcNow + ttl.Value : null,
            CorrelationId = correlationId,
            CreatedAtUtc = clock.UtcNow
        };
    }

    public void MarkCompleted(string? responsePayload, int? statusCode, IClock clock)
    {
        if (Status == IdempotencyStatus.Completed) return; // idempotent
        if (Status != IdempotencyStatus.InProgress && Status != IdempotencyStatus.Failed)
            throw new InvalidOperationException($"Cannot complete from {Status}.");

        StatusValue = IdempotencyStatus.Completed.Value;
        ResponsePayload = responsePayload;
        ResponseStatusCode = statusCode;
        CompletedAtUtc ??= clock.UtcNow;
        Touch(clock.UtcNow);
    }

    public void MarkFailed(string? reason, IClock clock)
    {
        if (Status == IdempotencyStatus.Failed) return;
        if (Status != IdempotencyStatus.InProgress)
            throw new InvalidOperationException($"Cannot fail from {Status}.");

        StatusValue = IdempotencyStatus.Failed.Value;
        ResponsePayload = reason;
        CompletedAtUtc ??= clock.UtcNow;
        Touch(clock.UtcNow);
    }

    public bool IsExpired(IClock clock) =>
        ExpiresAtUtc.HasValue && clock.UtcNow > ExpiresAtUtc.Value;
}

/// <summary>
/// Status of an IdempotencyRecord.
/// </summary>
public sealed class IdempotencyStatus : Enumeration
{
    public static readonly IdempotencyStatus InProgress = new(1, nameof(InProgress));
    public static readonly IdempotencyStatus Completed = new(2, nameof(Completed));
    public static readonly IdempotencyStatus Failed = new(3, nameof(Failed));

    private IdempotencyStatus(int value, string name) : base(value, name) { }

    public static IEnumerable<IdempotencyStatus> All() => GetAll<IdempotencyStatus>();

    public static IdempotencyStatus FromValue(int value) => FromValue<IdempotencyStatus>(value);

    public static IdempotencyStatus FromName(string name) => FromName<IdempotencyStatus>(name);
}
