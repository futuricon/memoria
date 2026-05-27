using Memoria.Shared.Kernel.Observability;

using Microsoft.AspNetCore.Http;

using Serilog.Context;

namespace Memoria.Api.Middleware;

/// <summary>
/// Initializes the per-request <see cref="OperationContext"/> for the API
/// entry point: generates a fresh <c>CorrelationId</c> (or honors an incoming
/// <c>X-Correlation-Id</c> header), echoes it back in the response, populates
/// <see cref="OperationContextAccessor"/>, and pushes the context properties
/// into Serilog's <see cref="LogContext"/> so every log line emitted during
/// the request is tagged.
/// <para>
/// Registered first in <c>UseApiPipeline</c> — before <c>UseExceptionHandler</c>
/// so even unhandled exceptions get the CorrelationId in their log lines and
/// ProblemDetails payload.
/// </para>
/// </summary>
internal sealed class OperationContextMiddleware
{
    private const string Header = "X-Correlation-Id";
    private const string ModuleName = "Api";

    private readonly RequestDelegate _next;

    public OperationContextMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        var correlationId = http.Request.Headers.TryGetValue(Header, out var incoming)
                            && !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : Guid.NewGuid().ToString("N");

        OperationContextAccessor.Current.Value = new OperationContext
        {
            CorrelationId = correlationId,
            Module = ModuleName,
        };

        http.Response.Headers[Header] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("Module", ModuleName))
        {
            try
            {
                await _next(http).ConfigureAwait(false);
            }
            finally
            {
                OperationContextAccessor.Current.Value = null;
            }
        }
    }
}
