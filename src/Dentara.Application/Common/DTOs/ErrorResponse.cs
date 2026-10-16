namespace Dentara.Application.Common.DTOs;

/// <summary>
/// Common API error response. Returned by the API layer when a Result is Failure.
/// Maps cleanly to RFC 7807 Problem Details if needed.
/// </summary>
public sealed record ErrorResponse(
    string ErrorCode,
    string Message,
    string? CorrelationId = null,
    IDictionary<string, string[]>? ValidationErrors = null);
