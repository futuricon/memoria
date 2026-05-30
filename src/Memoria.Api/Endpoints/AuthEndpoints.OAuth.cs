using Memoria.Api.Authentication;
using Memoria.Api.Configuration;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Memoria.Api.Endpoints;

/// <summary>
/// Kick-off endpoints for SPA Google/GitHub OAuth. The SPA navigates the
/// browser to <c>/api/v1/auth/{provider}/start?returnUrl=...</c>; we stash
/// the (validated) returnUrl in <see cref="AuthenticationProperties.Items"/>
/// and trigger a Challenge for the SPA-scheme handler. The handler bounces
/// to the provider; after consent the provider redirects to the callback
/// path configured on the scheme, which is short-circuited by
/// <see cref="SpaOAuthEvents"/> to issue a JWT pair.
/// </summary>
internal static class AuthOAuthEndpoints
{
    public static IEndpointRouteBuilder MapOAuthAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app
            .MapGroup("/api/v1/auth")
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy)
            .AllowAnonymous();

        group.MapGet("/google/start", (
                HttpContext ctx,
                IOptions<CorsOptions> cors,
                [FromQuery] string? returnUrl) =>
            StartChallengeAsync(ctx, cors.Value, OAuthAuthenticationConfiguration.GoogleSpaScheme, returnUrl));

        group.MapGet("/github/start", (
                HttpContext ctx,
                IOptions<CorsOptions> cors,
                [FromQuery] string? returnUrl) =>
            StartChallengeAsync(ctx, cors.Value, OAuthAuthenticationConfiguration.GitHubSpaScheme, returnUrl));

        return app;
    }

    private static async Task<IResult> StartChallengeAsync(
        HttpContext ctx,
        CorsOptions cors,
        string scheme,
        string? returnUrl)
    {
        if (!SpaOAuthEvents.TryValidateReturnUrl(returnUrl, cors, out var normalizedReturnUrl))
        {
            return Microsoft.AspNetCore.Http.Results.BadRequest(new
            {
                code = "auth.invalid_return_url",
                message = "returnUrl must match a configured CORS origin.",
            });
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = normalizedReturnUrl,
        };
        properties.Items[SpaOAuthEvents.ReturnUrlKey] = normalizedReturnUrl;

        await ctx.ChallengeAsync(scheme, properties).ConfigureAwait(false);
        return Microsoft.AspNetCore.Http.Results.Empty;
    }
}
