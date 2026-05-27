using MediatR;

using Memoria.Api.Authentication;
using Memoria.Api.Configuration;
using Memoria.Api.Results;
using Memoria.Reminders.Contracts.Queries;
using Memoria.Reviews.Contracts.Commands;
using Memoria.Reviews.Contracts.Dtos;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Memoria.Api.Endpoints;

public sealed record RecordReviewRequest(Guid? ReminderId, Rating Rating, string? Note);

internal static class CardsActivityEndpoints
{
    public static IEndpointRouteBuilder MapCardsActivityEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app
            .MapGroup("/api/v1/cards")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingConfiguration.DefaultPolicy);

        group.MapGet("/due-today", async (
                HttpContext ctx,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var result = await mediator
                    .Send(new GetDueRemindersForUserQuery(user.Id, today), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        group.MapPost("/{id:guid}/review", async (
                HttpContext ctx,
                Guid id,
                [FromBody] RecordReviewRequest req,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new RecordReviewCommand(
                        user.Id,
                        id,
                        req.ReminderId,
                        req.Rating,
                        req.Note), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        return app;
    }
}
