using System.Diagnostics;

using MediatR;

using Microsoft.Extensions.Logging;

namespace Memoria.Shared.Infrastructure.Behaviors;

/// <summary>
/// Timing behavior for MediatR handlers. Logs at <see cref="LogLevel.Debug"/>
/// for normal execution and escalates to <see cref="LogLevel.Warning"/> when
/// the handler exceeds <see cref="SlowThresholdMs"/>. Wrapped in try/finally
/// so we always emit the timing even when the handler throws.
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowThresholdMs = 500;

    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var sw = Stopwatch.StartNew();
        try
        {
            return await next().ConfigureAwait(false);
        }
        finally
        {
            sw.Stop();
            var name = typeof(TRequest).Name;
            var ms = sw.ElapsedMilliseconds;

            if (ms > SlowThresholdMs)
            {
                _logger.LogWarning(
                    "Slow MediatR handler {Request} took {ElapsedMs}ms (>{ThresholdMs}ms)",
                    name, ms, SlowThresholdMs);
            }
            else
            {
                _logger.LogDebug(
                    "MediatR handler {Request} took {ElapsedMs}ms",
                    name, ms);
            }
        }
    }
}
