namespace Dentara.Api.Middleware;

using Dentara.Application.Common.DTOs;
using Dentara.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

/// <summary>
/// Global exception handler. Catches all unhandled exceptions and converts them
/// to RFC 7807-style ProblemDetails responses.
///
/// Mapping rules:
///   - DomainException → 4xx with ErrorCode + Message (machine-readable for clients).
///   - Validation exceptions → 400 with VALIDATION_ERROR code.
///   - UnauthorizedAccessException → 401.
///   - Other → 500 with UNEXPECTED_ERROR (details only in Development environment).
///
/// CRITICAL per architecture spec: logs include tracking ids but MUST NOT include
/// OTP, payment-sensitive info, or secrets. This middleware logs only the exception
/// type, ErrorCode, and a sanitized message — never the full stack trace in production.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        IHostEnvironment environment,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _environment = environment;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorResponse, logLevel) = MapException(exception);

        // Extract correlation id from headers if present
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();

        // Log: include exception type + ErrorCode + sanitized message. Never log
        // the full exception.ToString() in production (may contain secrets).
        if (logLevel == LogLevel.Error)
        {
            _logger.LogError(exception,
                "Unhandled exception {ExceptionType} for {Method} {Path}: {ErrorCode} - {Message}",
                exception.GetType().Name,
                context.Request.Method,
                context.Request.Path,
                errorResponse.ErrorCode,
                errorResponse.Message);
        }
        else
        {
            _logger.LogWarning(exception,
                "Domain exception {ExceptionType} for {Method} {Path}: {ErrorCode} - {Message}",
                exception.GetType().Name,
                context.Request.Method,
                context.Request.Path,
                errorResponse.ErrorCode,
                errorResponse.Message);
        }

        var response = errorResponse with { CorrelationId = correlationId };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        // Add X-Correlation-Id to response headers for client-side tracing
        if (correlationId != null)
            context.Response.Headers["X-Correlation-Id"] = correlationId;

        var json = JsonSerializer.Serialize(response, _jsonOptions);
        await context.Response.WriteAsync(json);
    }

    private static (HttpStatusCode StatusCode, ErrorResponse Response, LogLevel Level) MapException(Exception ex)
    {
        return ex switch
        {
            // Domain exceptions — business rule violations. Map to specific HTTP codes.
            DomainException de => de.ErrorCode switch
            {
                // Booking
                "BOOKING_SLOT_UNAVAILABLE" or "BOOKING_RESOURCE_UNAVAILABLE" or "BOOKING_DENTIST_NOT_AVAILABLE"
                    => (HttpStatusCode.Conflict, new ErrorResponse(de.ErrorCode, de.Message), LogLevel.Warning),
                "BOOKING_INVALID_TRANSITION" or "BOOKING_APPOINTMENT_TERMINAL"
                    => (HttpStatusCode.BadRequest, new ErrorResponse(de.ErrorCode, de.Message), LogLevel.Warning),

                // Payments
                "PAYMENT_INVALID_TRANSITION"
                    => (HttpStatusCode.BadRequest, new ErrorResponse(de.ErrorCode, de.Message), LogLevel.Warning),
                "PAYMENT_REFUND_EXCEEDS_TOTAL"
                    => (HttpStatusCode.BadRequest, new ErrorResponse(de.ErrorCode, de.Message), LogLevel.Warning),
                "PAYMENT_INVALID_WEBHOOK_SIGNATURE" or "PAYMENT_WEBHOOK_AMOUNT_MISMATCH"
                    => (HttpStatusCode.UnprocessableEntity, new ErrorResponse(de.ErrorCode, de.Message), LogLevel.Warning),

                // Idempotency
                "IDEMPOTENCY_CONFLICT"
                    => (HttpStatusCode.Conflict, new ErrorResponse(de.ErrorCode, de.Message), LogLevel.Warning),

                // Identity
                "IDENTITY_PHONE_ALREADY_REGISTERED"
                    => (HttpStatusCode.Conflict, new ErrorResponse(de.ErrorCode, de.Message), LogLevel.Warning),
                "IDENTITY_INVALID_OTP"
                    => (HttpStatusCode.Unauthorized, new ErrorResponse(de.ErrorCode, de.Message), LogLevel.Warning),
                "IDENTITY_PHONE_NOT_VERIFIED"
                    => (HttpStatusCode.Forbidden, new ErrorResponse(de.ErrorCode, de.Message), LogLevel.Warning),

                // Catalog
                "CATALOG_SERVICE_NOT_ACTIVE" or "CATALOG_NO_RESOURCE_REQUIREMENTS"
                    => (HttpStatusCode.BadRequest, new ErrorResponse(de.ErrorCode, de.Message), LogLevel.Warning),

                // Scheduling
                "SCHEDULING_PUBLICATION_FAILED"
                    => (HttpStatusCode.BadRequest, new ErrorResponse(de.ErrorCode, de.Message), LogLevel.Warning),

                _ => (HttpStatusCode.BadRequest, new ErrorResponse(de.ErrorCode, de.Message), LogLevel.Warning)
            },

            // Validation
            ArgumentException => (HttpStatusCode.BadRequest,
                new ErrorResponse("VALIDATION_ERROR", ex.Message), LogLevel.Warning),

            // Unauthorized
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized,
                new ErrorResponse("UNAUTHORIZED", "Authentication required."), LogLevel.Warning),

            // Everything else — internal server error
            _ => (HttpStatusCode.InternalServerError,
                new ErrorResponse("UNEXPECTED_ERROR",
                    "An unexpected error occurred. Please contact support if the problem persists."),
                LogLevel.Error)
        };
    }
}
