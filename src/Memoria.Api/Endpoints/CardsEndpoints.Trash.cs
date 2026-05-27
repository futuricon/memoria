using MediatR;

using Memoria.Api.Authentication;
using Memoria.Api.Configuration;
using Memoria.Api.Results;
using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Contracts.Queries;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Memoria.Api.Endpoints;

internal static class CardsTrashEndpoints
{
    public static IEndpointRouteBuilder MapCardsTrashEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app
            .MapGroup("/api/v1/cards")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingConfiguration.DefaultPolicy);

        group.MapGet("/trash", async (
                HttpContext ctx,
                IMediator mediator,
                CancellationToken ct,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 10) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new GetTrashedCardsQuery(user.Id, page, pageSize), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        group.MapPost("/{id:guid}/restore", async (
                HttpContext ctx,
                IMediator mediator,
                Guid id,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new RestoreCardCommand(user.Id, id), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        group.MapDelete("/{id:guid}/permanent", async (
                HttpContext ctx,
                IMediator mediator,
                Guid id,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new PermanentlyDeleteCardCommand(user.Id, id), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResultNoContent();
            });

        return app;
    }
}
