using MediatR;

using Memoria.Shared.Kernel.Observability;

using Serilog.Context;

namespace Memoria.Shared.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that enriches every log line emitted from inside
/// a handler with the current <see cref="OperationContext"/> via Serilog's
/// <see cref="LogContext"/>. Idempotent vs the entry-point middleware: if the
/// same properties were already pushed by the API middleware or bot scope they
/// get pushed again with the same value — harmless and cheap. Critical for
/// flows where MediatR is invoked OUTSIDE an HTTP/bot entry point
/// (Hangfire jobs, background services) — those should populate
/// <see cref="OperationContextAccessor"/> before dispatch.
/// </summary>
public sealed class OperationContextBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var ctx = OperationContextAccessor.Current.Value;
        if (ctx is null)
        {
            return await next().ConfigureAwait(false);
        }

        using var corrScope = !string.IsNullOrEmpty(ctx.CorrelationId)
            ? LogContext.PushProperty("CorrelationId", ctx.CorrelationId)
            : null;
        using var modScope = !string.IsNullOrEmpty(ctx.Module)
            ? LogContext.PushProperty("Module", ctx.Module)
            : null;
        using var userScope = !string.IsNullOrEmpty(ctx.UserId)
            ? LogContext.PushProperty("UserId", ctx.UserId)
            : null;
        using var tgScope = !string.IsNullOrEmpty(ctx.TelegramUserId)
            ? LogContext.PushProperty("TelegramUserId", ctx.TelegramUserId)
            : null;

        return await next().ConfigureAwait(false);
    }
}
