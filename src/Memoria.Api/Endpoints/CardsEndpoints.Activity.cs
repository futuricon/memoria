using MediatR;

using Memoria.Api.Authentication;
using Memoria.Api.Configuration;
using Memoria.Api.Results;
using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Queries;
using Memoria.Reminders.Contracts.Queries;
using Memoria.Reviews.Contracts.Commands;
using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Queries;
using Memoria.Shared.Kernel.Results;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Memoria.Api.Endpoints;

public sealed record RecordReviewRequest(Guid? ReminderId, Rating Rating, string? Note);

/// <summary>
/// Composite DTO returned by SPA dashboard endpoints. Joins a card summary
/// with its aggregate review stats so the client gets everything in one round
/// trip.
/// </summary>
public sealed record CardWithGradeDto(
    Guid Id,
    string Title,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt,
    CardType Type,
    int ReviewCount,
    double? AvgRating,
    double? AvgAiScore);

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

        group.MapGet("/upcoming", async (
                HttpContext ctx,
                IMediator mediator,
                CancellationToken ct,
                [FromQuery] int take = 10) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new GetUpcomingRemindersForUserQuery(user.Id, take), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        group.MapGet("/worst", async (
                HttpContext ctx,
                IMediator mediator,
                CancellationToken ct,
                [FromQuery] int take = 5,
                [FromQuery] int minReviews = 3) =>
            {
                var user = ctx.GetCurrentUser();
                var worstResult = await mediator
                    .Send(new GetWorstCardsQuery(user.Id, take, minReviews), ct)
                    .ConfigureAwait(false);

                if (worstResult.IsFailure)
                {
                    return worstResult.ToHttpResult();
                }

                var stats = worstResult.Value!;
                if (stats.Count == 0)
                {
                    return Result<IReadOnlyList<CardWithGradeDto>>
                        .Success(Array.Empty<CardWithGradeDto>())
                        .ToHttpResult();
                }

                var joined = new List<CardWithGradeDto>(stats.Count);
                foreach (var s in stats)
                {
                    var cardResult = await mediator
                        .Send(new GetCardByIdQuery(user.Id, s.CardId, IncludeDeleted: false), ct)
                        .ConfigureAwait(false);

                    if (cardResult.IsFailure)
                    {
                        // Card may have been soft-deleted between aggregation and join; skip.
                        continue;
                    }

                    var c = cardResult.Value!;
                    joined.Add(new CardWithGradeDto(
                        c.Id, c.Title, c.Tags, c.CreatedAt, c.Type,
                        s.ReviewCount, s.AvgRating, s.AvgAiScore));
                }

                return Result<IReadOnlyList<CardWithGradeDto>>
                    .Success(joined)
                    .ToHttpResult();
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
