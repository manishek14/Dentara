namespace Dentara.Application.Common.Behaviors;

using Dentara.Application.Common.Results;
using MediatR;

/// <summary>
/// MediatR pipeline behavior that runs FluentValidation-style validation on
/// requests that implement IValidatable. If validation fails, returns a
/// Failure result with VALIDATION_ERROR code without invoking the handler.
///
/// Validation is for INPUT CONTRACT checks (non-null, length, format).
/// Business rule violations (e.g. "cannot cancel completed appointment")
/// happen in the handler and surface as DomainException-derived exceptions.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);
        var results = new List<ValidationResult>();
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            results.Add(result);
        }

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count != 0)
        {
            var errorMessage = string.Join("; ", failures.Select(f => f.ErrorMessage));
            // Use reflection to construct the failure result since TResponse is generic
            var failureMethod = typeof(TResponse)
                .GetMethod(nameof(Result<object>.Failure), new[] { typeof(string), typeof(string) })
                ?? typeof(Result).GetMethod(nameof(Result.Failure), new[] { typeof(string), typeof(string) });

            if (failureMethod != null)
            {
                var failure = (TResponse)failureMethod.Invoke(null, new object[] { "VALIDATION_ERROR", errorMessage })!;
                return failure;
            }
        }

        return await next(cancellationToken);
    }
}

/// <summary>
/// Minimal validator interface (so we don't force a FluentValidation dependency
/// on the Application layer). Infrastructure/Api can register FluentValidation
/// adapters that implement this interface.
/// </summary>
public interface IValidator<T>
{
    Task<ValidationResult> ValidateAsync(ValidationContext<T> context, CancellationToken cancellationToken = default);
}

public sealed class ValidationContext<T>
{
    public T InstanceToValidate { get; }
    public ValidationContext(T instance) => InstanceToValidate = instance;
}

public sealed class ValidationResult
{
    public List<ValidationFailure> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
}

public sealed class ValidationFailure
{
    public string PropertyName { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = "VALIDATION_ERROR";
}
