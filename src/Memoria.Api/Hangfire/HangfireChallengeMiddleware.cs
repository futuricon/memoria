using Memoria.Api.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Memoria.Api.Hangfire;

/// <summary>
/// For <c>/jobs*</c>: applies the Hangfire cookie scheme explicitly (the app's
/// default auth scheme is JWT, so the dashboard would otherwise never see the
/// OAuth-signed-in user), then maps the dashboard's 401/403 into redirects —
/// <c>/jobs/login</c> when anonymous, <c>/jobs/forbidden</c> when signed in but
/// not on the allowlist. Registered BEFORE the dashboard.
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

        var isJobs = ctx.Request.Path.StartsWithSegments("/jobs");
        if (isJobs)
        {
            var auth = await ctx.AuthenticateAsync(OAuthAuthenticationConfiguration.CookieScheme).ConfigureAwait(false);
            if (auth.Succeeded && auth.Principal is not null)
            {
                ctx.User = auth.Principal;
            }
        }

        await _next(ctx).ConfigureAwait(false);

        if (!isJobs || ctx.Response.HasStarted)
        {
            return;
        }

        switch (ctx.Response.StatusCode)
        {
            // Signed in via cookie but rejected by the allowlist → forbidden,
            // not the login loop.
            case StatusCodes.Status401Unauthorized when ctx.User.Identity?.IsAuthenticated == true:
                ctx.Response.Redirect("/jobs/forbidden");
                break;

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
