using System.Security.Claims;
using System.Text;

using MediatR;

using Memoria.Api.Configuration;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Contracts.Dtos;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoria.Api.Authentication;

/// <summary>
/// Short-circuits the OAuth handler before the default sign-in step:
/// extracts the provider profile, asks Users module to mint a JWT pair, then
/// redirects the browser to the SPA callback URL with the tokens in the URL
/// fragment. Fragments are never sent to servers, so this keeps the tokens
/// out of nginx access logs and TLS-terminator logs along the way.
/// </summary>
internal static class SpaOAuthEvents
{
    public const string ReturnUrlKey = "spa.returnUrl";

    public static Task HandleGoogleTicket(TicketReceivedContext ctx) =>
        HandleTicketAsync(ctx, providerName: "Google", externalIdClaim: ClaimTypes.NameIdentifier);

    public static Task HandleGitHubTicket(TicketReceivedContext ctx) =>
        HandleTicketAsync(ctx, providerName: "GitHub", externalIdClaim: ClaimTypes.NameIdentifier);

    private static async Task HandleTicketAsync(
        TicketReceivedContext ctx,
        string providerName,
        string externalIdClaim)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var http = ctx.HttpContext;
        var mediator = http.RequestServices.GetRequiredService<IMediator>();
        var logger = http.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("SpaOAuthEvents");

        var principal = ctx.Principal
            ?? throw new InvalidOperationException("OAuth ticket arrived without a principal.");

        var externalId = principal.FindFirstValue(externalIdClaim);
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var name = principal.FindFirstValue(ClaimTypes.Name)
            ?? principal.FindFirstValue("name")
            ?? email
            ?? providerName;

        // Google's profile endpoint sets email_verified=true for the primary
        // verified email on the account; GitHub returns the user's primary
        // verified email via the OAuth scope user:email, so treat both as
        // verified when an email is present.
        var emailVerified = !string.IsNullOrWhiteSpace(email);

        if (string.IsNullOrWhiteSpace(externalId))
        {
            logger.LogWarning(
                "SpaOAuthEvents: {Provider} ticket lacked NameIdentifier — denying", providerName);
            ctx.Fail("Missing provider id.");
            ctx.HandleResponse();
            await RedirectToSpaWithErrorAsync(ctx, "missing_id");
            return;
        }

        var result = await mediator.Send(new AuthenticateOAuthCommand(
            Provider: providerName,
            ExternalId: externalId,
            Email: email,
            EmailVerified: emailVerified,
            DisplayName: name), http.RequestAborted).ConfigureAwait(false);

        if (result.IsFailure)
        {
            logger.LogWarning(
                "SpaOAuthEvents: {Provider} sign-in failed: {Code} {Message}",
                providerName, result.Error!.Code, result.Error.Message);
            ctx.HandleResponse();
            await RedirectToSpaWithErrorAsync(ctx, result.Error.Code);
            return;
        }

        ctx.HandleResponse();
        await RedirectToSpaWithTokensAsync(ctx, result.Value!);
    }

    private static Task RedirectToSpaWithTokensAsync(TicketReceivedContext ctx, JwtTokenPairDto pair)
    {
        var returnUrl = ResolveReturnUrl(ctx);
        var fragment = new StringBuilder("#");
        fragment.Append("access=").Append(Uri.EscapeDataString(pair.AccessToken));
        fragment.Append("&refresh=").Append(Uri.EscapeDataString(pair.RefreshToken));
        fragment.Append("&accessExpires=").Append(pair.AccessExpiresAt.ToString("O"));
        fragment.Append("&refreshExpires=").Append(pair.RefreshExpiresAt.ToString("O"));
        ctx.Response.Redirect(returnUrl + fragment.ToString());
        return Task.CompletedTask;
    }

    private static Task RedirectToSpaWithErrorAsync(TicketReceivedContext ctx, string code)
    {
        var returnUrl = ResolveReturnUrl(ctx);
        ctx.Response.Redirect(returnUrl + "#error=" + Uri.EscapeDataString(code));
        return Task.CompletedTask;
    }

    private static string ResolveReturnUrl(TicketReceivedContext ctx)
    {
        // Stored by the /start endpoint before triggering the Challenge.
        if (ctx.Properties is { } props
            && props.Items.TryGetValue(ReturnUrlKey, out var stored)
            && !string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        // Fallback: first allowed CORS origin + "/auth/callback".
        var cors = ctx.HttpContext.RequestServices
            .GetRequiredService<IOptions<CorsOptions>>().Value;
        var origin = cors.AllowedOrigins.FirstOrDefault(o =>
            o.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            ?? (cors.AllowedOrigins.Count > 0 ? cors.AllowedOrigins[0] : "/");
        return origin.TrimEnd('/') + "/auth/callback";
    }

    /// <summary>
    /// Validates a SPA-supplied return URL against the CORS allow-list to
    /// stop open-redirect abuse — without it, an attacker could
    /// <c>?returnUrl=https://evil/</c> and harvest tokens from the fragment.
    /// </summary>
    public static bool TryValidateReturnUrl(
        string? candidate,
        CorsOptions cors,
        out string normalized)
    {
        normalized = string.Empty;

        if (string.IsNullOrWhiteSpace(candidate)
            || !Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var origin = uri.GetLeftPart(UriPartial.Authority);
        foreach (var allowed in cors.AllowedOrigins)
        {
            if (string.Equals(allowed.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase))
            {
                normalized = candidate;
                return true;
            }
        }

        return false;
    }
}
