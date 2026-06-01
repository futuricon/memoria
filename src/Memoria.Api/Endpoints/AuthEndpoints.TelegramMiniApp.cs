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

public sealed record TelegramMiniAppLoginRequest(string InitData);

internal static class AuthTelegramMiniAppEndpoint
{
    public static IEndpointRouteBuilder MapTelegramMiniAppEndpoint(this IEndpointRouteBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/api/v1/auth/telegram-miniapp", async (
                [FromBody] TelegramMiniAppLoginRequest body,
                TelegramMiniAppInitDataValidator validator,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var validated = validator.Validate(body.InitData);
                if (validated.IsFailure) return validated.ToHttpResult();

                var p = validated.Value!;
                var displayName = string.IsNullOrEmpty(p.User.LastName)
                    ? p.User.FirstName
                    : $"{p.User.FirstName} {p.User.LastName}";

                var result = await mediator.Send(new AuthenticateTelegramWidgetCommand(
                    p.User.Id.ToString(CultureInfo.InvariantCulture),
                    displayName), ct).ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy)
            .AllowAnonymous();

        return group;
    }
}