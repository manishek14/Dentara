namespace Dentara.Infrastructure.BackgroundJobs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for all Dentara background workers. Wraps IHostedService with
/// common concerns: graceful shutdown via CancellationToken, error logging
/// with retry, and a configurable interval between iterations.
///
/// Subclasses implement ExecuteIterationAsync — called repeatedly until the
/// cancellation token is signaled. If an iteration throws, the exception is
/// logged and the worker waits for the configured error delay before retrying.
/// </summary>
public abstract class WorkerBase : BackgroundService
{
    private readonly TimeSpan _interval;
    private readonly TimeSpan _errorDelay;
    private readonly ILogger _logger;

    protected WorkerBase(TimeSpan interval, ILogger logger, TimeSpan? errorDelay = null)
    {
        _interval = interval;
        _errorDelay = errorDelay ?? TimeSpan.FromMinutes(1);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{WorkerName} started; interval = {Interval}",
            GetType().Name, _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExecuteIterationAsync(stoppingToken);

                // Wait for the next iteration. Use Task.Delay with the cancellation
                // token so shutdown is responsive.
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown — exit the loop
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "{WorkerName} iteration failed; retrying in {ErrorDelay}",
                    GetType().Name, _errorDelay);
                try
                {
                    await Task.Delay(_errorDelay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("{WorkerName} stopped", GetType().Name);
    }

    /// <summary>
    /// Called once per iteration. Implementations should be idempotent — if a
    /// previous iteration was interrupted mid-way, the next one must safely
    /// resume or skip already-processed items.
    /// </summary>
    protected abstract Task ExecuteIterationAsync(CancellationToken stoppingToken);
}
