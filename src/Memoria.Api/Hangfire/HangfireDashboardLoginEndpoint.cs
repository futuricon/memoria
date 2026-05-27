using AspNet.Security.OAuth.GitHub;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Memoria.Api.Hangfire;

/// <summary>
/// Лёгкая HTML-страница входа на /jobs (две кнопки Google/GitHub) + два
/// шлюза, которые поднимают OAuth-challenge для выбранного провайдера,
/// плюс /jobs/forbidden — landing для пользователей вне allowlist.
/// </summary>
internal static class HangfireDashboardLoginEndpoint
{
    public static IEndpointRouteBuilder MapHangfireDashboardLogin(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/jobs/login", (HttpContext ctx) =>
        {
            var returnUrl = ctx.Request.Query["returnUrl"].FirstOrDefault() ?? "/jobs";
            var encoded = Uri.EscapeDataString(returnUrl);
            var html =
                "<!doctype html>\n" +
                "<html><head><meta charset=\"utf-8\"><title>Memoria — Hangfire sign-in</title></head>" +
                "<body style=\"font-family: system-ui, sans-serif; max-width: 480px; margin: 6em auto;\">" +
                "<h2>Hangfire dashboard sign-in</h2>" +
                "<p>Select a provider to authenticate:</p>" +
                "<p>" +
                $"<a href=\"/jobs/signin/google?returnUrl={encoded}\" " +
                "style=\"display:inline-block;padding:.6em 1em;border:1px solid #888;border-radius:6px;text-decoration:none;color:#333;margin-right:1em;\">Continue with Google</a>" +
                $"<a href=\"/jobs/signin/github?returnUrl={encoded}\" " +
                "style=\"display:inline-block;padding:.6em 1em;border:1px solid #888;border-radius:6px;text-decoration:none;color:#333;\">Continue with GitHub</a>" +
                "</p></body></html>";
            return Microsoft.AspNetCore.Http.Results.Content(html, "text/html");
        }).AllowAnonymous();

        app.MapGet("/jobs/signin/google", (HttpContext ctx) =>
        {
            var returnUrl = ctx.Request.Query["returnUrl"].FirstOrDefault() ?? "/jobs";
            return Microsoft.AspNetCore.Http.Results.Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl },
                authenticationSchemes: new[] { GoogleDefaults.AuthenticationScheme });
        }).AllowAnonymous();

        app.MapGet("/jobs/signin/github", (HttpContext ctx) =>
        {
            var returnUrl = ctx.Request.Query["returnUrl"].FirstOrDefault() ?? "/jobs";
            return Microsoft.AspNetCore.Http.Results.Challenge(
                new AuthenticationProperties { RedirectUri = returnUrl },
                authenticationSchemes: new[] { GitHubAuthenticationDefaults.AuthenticationScheme });
        }).AllowAnonymous();

        app.MapGet("/jobs/forbidden", () =>
            Microsoft.AspNetCore.Http.Results.Content(
                "<!doctype html><html><body style=\"font-family: system-ui, sans-serif; max-width: 480px; margin: 6em auto;\">" +
                "<h2>Access denied</h2><p>Your email is not in the Hangfire dashboard allowlist.</p></body></html>",
                "text/html"))
            .AllowAnonymous();

        return app;
    }
}
