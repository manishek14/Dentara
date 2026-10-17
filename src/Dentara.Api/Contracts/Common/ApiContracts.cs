namespace Dentara.Api.Contracts.Common;

/// <summary>
/// Standard error response returned by all API endpoints on failure.
/// Maps to the Application layer's ErrorResponse DTO.
/// </summary>
public sealed record ApiErrorResponse(
    string ErrorCode,
    string Message,
    string? CorrelationId = null);

/// <summary>
/// Wraps an idempotency key header for mutation endpoints. Clients should
/// send X-Idempotency-Key on retries to ensure the operation is not double-executed.
/// </summary>
public static class ApiHeaders
{
    public const string IdempotencyKey = "X-Idempotency-Key";
    public const string CorrelationId = "X-Correlation-Id";
}
