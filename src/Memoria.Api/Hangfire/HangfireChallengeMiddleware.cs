using Microsoft.AspNetCore.Http;

namespace Memoria.Api.Hangfire;

/// <summary>
/// Перехватывает 401/403 от <c>HangfireDashboardAuthorizationFilter</c> на пути
/// <c>/jobs*</c> и редиректит на <c>/jobs/login</c> / <c>/jobs/forbidden</c>.
/// Регистрируется ДО <c>UseHangfireDashboard</c>: middleware пропускает запрос
/// вниз, видит итоговый статус и подменяет ответ на 302.
/// </summary>
internal sealed class HangfireChallengeMiddleware
{
    private readonly RequestDelegate _next;

    public HangfireChallengeMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        await _next(ctx).ConfigureAwait(false);

        if (!ctx.Request.Path.StartsWithSegments("/jobs"))
        {
            return;
        }

        if (ctx.Response.HasStarted)
        {
            return;
        }

        switch (ctx.Response.StatusCode)
        {
            case StatusCodes.Status401Unauthorized:
            {
                var returnUrl = Uri.EscapeDataString(ctx.Request.Path + ctx.Request.QueryString);
                ctx.Response.Redirect($"/jobs/login?returnUrl={returnUrl}");
                break;
            }
            case StatusCodes.Status403Forbidden:
                ctx.Response.Redirect("/jobs/forbidden");
                break;
        }
    }
}
