using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Memoria.Api.Middleware;

/// <summary>
/// Logs every <c>400 Bad Request</c> the API returns. Minimal API model
/// binding (broken JSON body, type mismatches like <c>TimeOnly</c> expecting
/// <c>HH:mm:ss</c>, missing required fields) short-circuits inside the
/// framework's request delegate and returns 400 without throwing — so
/// <see cref="GlobalExceptionHandler"/> never sees it and the only trace
/// would be the generic Serilog request-logging line.
/// <para>
/// This middleware emits a <c>Warning</c> with method + path + query so
/// these silent client errors are findable in logs. Other 4xx (401, 403,
/// 429) already have their own logging pipelines and are intentionally
/// ignored here to keep noise down.
/// </para>
/// </summary>
internal sealed class BadRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<BadRequestLoggingMiddleware> _logger;

    public BadRequestLoggingMiddleware(RequestDelegate next, ILogger<BadRequestLoggingMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(logger);
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        await _next(http).ConfigureAwait(false);

        if (http.Response.StatusCode == StatusCodes.Status400BadRequest)
        {
            _logger.LogWarning(
                "Bad request: {Method} {Path}{QueryString} returned 400",
                http.Request.Method,
                http.Request.Path.Value,
                http.Request.QueryString.HasValue ? http.Request.QueryString.Value : string.Empty);
        }
    }
}
