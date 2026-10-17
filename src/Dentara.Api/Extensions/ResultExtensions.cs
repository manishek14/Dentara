namespace Dentara.Api.Extensions;

using Dentara.Application.Common.Results;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Extension methods for mapping Result<T> to ASP.NET Core MVC IActionResult.
/// Centralizes the success/failure response pattern across all controllers.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Maps a Result<T> to IActionResult:
    ///   - Success → 200 OK with the value
    ///   - Failure → 4xx with ErrorResponse (ErrorCode + Message)
    /// </summary>
    public static IActionResult ToActionResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return new OkObjectResult(result.Value);

        var statusCode = MapErrorCodeToStatus(result.ErrorCode);
        var errorResponse = new
        {
            errorCode = result.ErrorCode,
            message = result.ErrorMessage
        };
        return new ObjectResult(errorResponse) { StatusCode = (int)statusCode };
    }

    /// <summary>
    /// Maps a Result (no value) to IActionResult.
    /// </summary>
    public static IActionResult ToActionResult(this Result result)
    {
        if (result.IsSuccess)
            return new NoContentResult();

        var statusCode = MapErrorCodeToStatus(result.ErrorCode);
        var errorResponse = new
        {
            errorCode = result.ErrorCode,
            message = result.ErrorMessage
        };
        return new ObjectResult(errorResponse) { StatusCode = (int)statusCode };
    }

    private static System.Net.HttpStatusCode MapErrorCodeToStatus(string? errorCode)
    {
        return errorCode switch
        {
            // Not found
            "NOTFOUND_APPOINTMENT" or "NOTFOUND_USER" or "NOTFOUND_PAYMENT"
                => System.Net.HttpStatusCode.NotFound,

            // Validation errors
            "VALIDATION_ERROR"
                => System.Net.HttpStatusCode.BadRequest,

            // Business rule violations
            "BOOKING_SLOT_UNAVAILABLE" or "BOOKING_RESOURCE_UNAVAILABLE"
                => System.Net.HttpStatusCode.Conflict,
            "BOOKING_INVALID_TRANSITION" or "BOOKING_APPOINTMENT_TERMINAL"
                => System.Net.HttpStatusCode.BadRequest,
            "BOOKING_DENTIST_NOT_AVAILABLE"
                => System.Net.HttpStatusCode.Conflict,

            "PAYMENT_INVALID_TRANSITION" or "PAYMENT_REFUND_EXCEEDS_TOTAL"
                => System.Net.HttpStatusCode.BadRequest,
            "PAYMENT_INVALID_WEBHOOK_SIGNATURE" or "PAYMENT_WEBHOOK_AMOUNT_MISMATCH"
                => System.Net.HttpStatusCode.UnprocessableEntity,

            "IDEMPOTENCY_CONFLICT"
                => System.Net.HttpStatusCode.Conflict,

            "IDENTITY_PHONE_ALREADY_REGISTERED"
                => System.Net.HttpStatusCode.Conflict,
            "IDENTITY_INVALID_OTP"
                => System.Net.HttpStatusCode.Unauthorized,
            "IDENTITY_PHONE_NOT_VERIFIED"
                => System.Net.HttpStatusCode.Forbidden,

            "CATALOG_SERVICE_NOT_ACTIVE" or "CATALOG_NO_RESOURCE_REQUIREMENTS"
                => System.Net.HttpStatusCode.BadRequest,

            "SCHEDULING_PUBLICATION_FAILED"
                => System.Net.HttpStatusCode.BadRequest,

            "INVALID_OPERATION"
                => System.Net.HttpStatusCode.BadRequest,

            "UNAUTHORIZED"
                => System.Net.HttpStatusCode.Unauthorized,

            // Fallback
            _ => System.Net.HttpStatusCode.BadRequest
        };
    }
}
