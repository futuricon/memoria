using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Memoria.Api.Middleware;

/// <summary>
/// Превращает любое непойманное исключение в ProblemDetails 500 + лог Error.
/// Внутренние детали исключения наружу не утекают; стектрейс остаётся в логах.
/// Регистрируется через <c>AddExceptionHandler&lt;GlobalExceptionHandler&gt;</c>
/// + <c>app.UseExceptionHandler()</c>.
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        _logger.LogError(exception,
            "Unhandled exception while processing {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        var problem = new ProblemDetails
        {
            Title = "InternalServerError",
            Detail = "An unexpected error occurred. Please try again later.",
            Status = StatusCodes.Status500InternalServerError,
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response
            .WriteAsJsonAsync(problem, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
