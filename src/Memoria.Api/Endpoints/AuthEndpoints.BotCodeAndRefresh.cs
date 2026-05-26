using MediatR;

using Memoria.Api.Configuration;
using Memoria.Api.Results;
using Memoria.Users.Contracts.Commands;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Memoria.Api.Endpoints;

public sealed record BotCodeRequest(string Code);
public sealed record RefreshRequest(string RefreshToken);

internal static class AuthBotCodeAndRefreshEndpoints
{
    public static IEndpointRouteBuilder MapBotCodeAndRefreshEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app
            .MapGroup("/api/v1/auth")
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy)
            .AllowAnonymous();

        group.MapPost("/bot-code", async (
                [FromBody] BotCodeRequest req,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var result = await mediator
                    .Send(new ExchangeBotLoginCodeCommand(req.Code), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        group.MapPost("/refresh", async (
                [FromBody] RefreshRequest req,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var result = await mediator
                    .Send(new RefreshAccessTokenCommand(req.RefreshToken), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        return app;
    }
}
