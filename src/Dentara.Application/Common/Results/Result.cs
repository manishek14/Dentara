namespace Dentara.Application.Common.Results;

using Dentara.Domain.Exceptions;

/// <summary>
/// Result of a command or query. Either Success with payload, or Failure with
/// an error code + message. The API layer maps Failure to HTTP 4xx/5xx.
///
/// Failure reasons:
///   - DomainException-derived: business rule violations (BOOKING_SLOT_UNAVAILABLE, etc.)
///   - ValidationError: input contract failures (VALIDATION_*)
///   - NotFoundError: aggregate not found (NOTFOUND_*)
///   - ConcurrencyError: optimistic concurrency conflict (CONCURRENCY_*)
///   - UnexpectedError: any other exception (UNEXPECTED_*)
/// </summary>
public abstract record Result
{
    public bool IsSuccess { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static Result<T> Success<T>(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure<T>(string errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    public static Result Success() => new UnitResult { IsSuccess = true };
    public static Result Failure(string errorCode, string errorMessage) => new UnitResult
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };
}

public sealed record Result<T> : Result
{
    public T? Value { get; init; }

    public T GetValueOrThrow() =>
        IsSuccess ? Value! : throw new InvalidOperationException(
            $"Cannot get value from failed result: {ErrorCode} - {ErrorMessage}");

    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };

    public new static Result<T> Failure(string errorCode, string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage
    };

    public static Result<T> FromException(Exception ex) => ex switch
    {
        DomainException de => Failure(de.ErrorCode, de.Message),
        ArgumentException ae => Failure("VALIDATION_ERROR", ae.Message),
        InvalidOperationException ioe => Failure("INVALID_OPERATION", ioe.Message),
        _ => Failure("UNEXPECTED_ERROR", $"An unexpected error occurred: {ex.Message}")
    };
}

public sealed record UnitResult : Result;
