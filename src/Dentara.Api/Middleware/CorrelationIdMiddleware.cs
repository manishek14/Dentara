namespace Dentara.Api.Middleware;

using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

/// <summary>
/// Adds a correlation id to every request. If the client sends X-Correlation-Id,
/// we use it; otherwise we generate a new GUID. The id is added to the response
/// headers for end-to-end tracing.
///
/// This id propagates through:
///   - HTTP response headers (X-Correlation-Id)
///   - Application layer (via IHasCorrelationId on commands)
///   - Domain events (CorrelationId property)
///   - Audit log (AppointmentEvents.CorrelationId)
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");

        // Store in Items so other middleware/controllers can access it
        context.Items[CorrelationIdHeader] = correlationId;

        // Add to response headers
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
                context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        await _next(context);
    }
}

/// <summary>
/// Extension methods for accessing the correlation id from the current HTTP context.
/// </summary>
public static class CorrelationIdExtensions
{
    private const string CorrelationIdHeader = "X-Correlation-Id";

    public static string? GetCorrelationId(this HttpContext context)
    {
        return context.Items.TryGetValue(CorrelationIdHeader, out var value)
            ? value as string
            : null;
    }
}
