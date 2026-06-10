using System.Text;

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
/// This middleware buffers the response body so the ProblemDetails payload
/// (or FluentValidation error message surfaced via <c>ValidationBehavior</c>)
/// is captured and emitted as a <c>Warning</c> alongside method + path.
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

        var originalBody = http.Response.Body;
        using var buffer = new MemoryStream();
        http.Response.Body = buffer;

        try
        {
            await _next(http).ConfigureAwait(false);
        }
        finally
        {
            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody).ConfigureAwait(false);
            http.Response.Body = originalBody;
        }

        if (http.Response.StatusCode == StatusCodes.Status400BadRequest)
        {
            buffer.Position = 0;
            var body = await new StreamReader(buffer, Encoding.UTF8, leaveOpen: true).ReadToEndAsync()
                .ConfigureAwait(false);

            _logger.LogWarning(
                "Bad request: {Method} {Path}{QueryString} returned 400 — {Body}",
                http.Request.Method,
                http.Request.Path.Value,
                http.Request.QueryString.HasValue ? http.Request.QueryString.Value : string.Empty,
                body);
        }
    }
}
