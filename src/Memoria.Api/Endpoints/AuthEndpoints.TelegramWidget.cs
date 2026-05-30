using System.Globalization;

using MediatR;

using Memoria.Api.Authentication;
using Memoria.Api.Configuration;
using Memoria.Api.Results;
using Memoria.Users.Contracts.Commands;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Memoria.Api.Endpoints;

internal static class AuthTelegramWidgetEndpoint
{
    public static IEndpointRouteBuilder MapTelegramWidgetEndpoint(this IEndpointRouteBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/api/v1/auth/telegram-widget", async (
                [FromBody] Dictionary<string, string> body,
                TelegramWidgetValidator validator,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var validated = validator.Validate(body);
                if (validated.IsFailure) return validated.ToHttpResult();

                var p = validated.Value!;
                var displayName = string.IsNullOrEmpty(p.LastName)
                    ? p.FirstName
                    : $"{p.FirstName} {p.LastName}";

                var result = await mediator.Send(new AuthenticateTelegramWidgetCommand(
                    p.Id.ToString(CultureInfo.InvariantCulture),
                    displayName), ct).ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy)
            .AllowAnonymous();

        // Authenticated SPA user requests a one-time link token + deep-link to
        // attach their Telegram identity (Settings page → "Link Telegram").
        group.MapPost("/api/v1/auth/telegram-linking/start", async (
                HttpContext ctx,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new StartTelegramLinkingCommand(user.Id), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            })
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy);

        return group;
    }
}
