namespace Dentara.Application.Common.Behaviors;

using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

/// <summary>
/// MediatR pipeline behavior that logs every request: name, elapsed time,
/// and whether it succeeded. The correlation id (if present in the request
/// via IHasCorrelationId) is included for end-to-end tracing.
///
/// Per the architecture spec: logs include tracking ids but must NOT include
/// OTP, payment-sensitive info, or secrets. Behavior logs only the request
/// TYPE NAME and elapsed time, never the request PAYLOAD.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var correlationId = (request as IHasCorrelationId)?.CorrelationId;

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestType"] = requestName,
            ["CorrelationId"] = correlationId
        });

        var sw = Stopwatch.StartNew();
        try
        {
            _logger.LogInformation("Handling {RequestName}", requestName);
            var response = await next(cancellationToken);
            sw.Stop();
            _logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms (success)",
                requestName, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _logger.LogError(ex, "Handled {RequestName} in {ElapsedMs}ms (failed: {ExceptionType})",
                requestName, sw.ElapsedMilliseconds, ex.GetType().Name);
            throw;
        }
    }
}

/// <summary>
/// Marker interface for requests that carry a correlation id for end-to-end tracing.
/// </summary>
public interface IHasCorrelationId
{
    string? CorrelationId { get; }
}
