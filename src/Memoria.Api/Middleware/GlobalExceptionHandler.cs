using FluentValidation;

using Memoria.Shared.Kernel.Observability;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Memoria.Api.Middleware;

/// <summary>
/// Превращает любое непойманное исключение в ProblemDetails + лог Error.
/// Внутренние детали исключения наружу не утекают; стектрейс остаётся в логах.
/// Корреляционный ID берётся из <see cref="OperationContextAccessor"/> (его
/// заполняет <see cref="OperationContextMiddleware"/> в самом начале pipeline-а)
/// и попадает и в лог, и в ProblemDetails (Extensions + X-Correlation-Id header).
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

        var ctx = OperationContextAccessor.Current.Value;

        _logger.LogError(exception,
            "Unhandled exception in {Module} | CorrelationId={CorrelationId} | {Method} {Path}",
            ctx?.Module,
            ctx?.CorrelationId,
            httpContext.Request.Method,
            httpContext.Request.Path);

        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation error"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            _ => (StatusCodes.Status500InternalServerError, "Internal server error"),
        };

        var problem = new ProblemDetails
        {
            Title = title,
            Status = status,
            Detail = "An unexpected error occurred. Please contact support.",
            Extensions =
            {
                ["correlationId"] = ctx?.CorrelationId,
                ["traceId"] = httpContext.TraceIdentifier,
            },
        };

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        httpContext.Response.Headers["X-Correlation-Id"] = ctx?.CorrelationId ?? "unknown";

        await httpContext.Response
            .WriteAsJsonAsync(problem, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}
