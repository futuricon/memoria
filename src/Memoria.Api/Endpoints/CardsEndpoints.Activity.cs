using MediatR;

using Memoria.AI.Contracts.Abstractions;
using Memoria.AI.Contracts.Dtos;
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

/// <summary>
/// Body of <c>POST /api/v1/cards/{id}/review</c>. The AI fields are filled by
/// the SPA when the user submitted a free-text answer and accepted the
/// AI-suggested rating (<c>AutoGraded=true</c>); they're <c>null</c> for
/// manual Note ratings or when the user overrides the AI suggestion.
/// </summary>
public sealed record RecordReviewRequest(
    Guid? ReminderId,
    Rating Rating,
    string? Note,
    string? AnswerText = null,
    int? AiScore = null,
    string? AiFeedback = null,
    bool AutoGraded = false);

/// <summary>Body of <c>POST /api/v1/cards/{id}/grade-answer</c>.</summary>
public sealed record GradeAnswerRequest(string UserAnswer);

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

/// <summary>
/// Composite DTO for the "stuck cards" dashboard widget — a candidate from
/// Reviews enriched with the card title and current reminder stage.
/// </summary>
public sealed record StuckCardDto(
    Guid CardId,
    string Title,
    int ConsecutiveForgotCount,
    DateTime LastReviewedAt,
    int? CurrentStage);

/// <summary>
/// Composite DTO for the "hardest tags" widget — average normalized rating
/// across all cards bearing the tag, plus how many cards / reviews fed it.
/// </summary>
public sealed record TagAverageDto(
    string Tag,
    int CardCount,
    int ReviewCount,
    double AvgScore);

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

        group.MapGet("/pending-ratings", async (
                HttpContext ctx,
                IMediator mediator,
                CancellationToken ct,
                [FromQuery] int take = 10) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new GetPendingRatingsForUserQuery(user.Id, take), ct)
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

        group.MapGet("/streak", async (
                HttpContext ctx,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new GetStreakQuery(user.Id), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        group.MapGet("/rating-distribution", async (
                HttpContext ctx,
                IMediator mediator,
                CancellationToken ct,
                [FromQuery] int days = 30) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new GetRatingDistributionQuery(user.Id, days), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        group.MapGet("/activity-heatmap", async (
                HttpContext ctx,
                IMediator mediator,
                CancellationToken ct,
                [FromQuery] int days = 90) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new GetActivityHeatmapQuery(user.Id, days), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        group.MapGet("/stuck", async (
                HttpContext ctx,
                IMediator mediator,
                CancellationToken ct,
                [FromQuery] int take = 10,
                [FromQuery] int minConsecutiveForgot = 3,
                [FromQuery] int maxStage = 2) =>
            {
                var user = ctx.GetCurrentUser();

                var candidatesResult = await mediator
                    .Send(new GetStuckCardCandidatesQuery(
                        user.Id, minConsecutiveForgot, Take: take * 3), ct)
                    .ConfigureAwait(false);

                if (candidatesResult.IsFailure)
                {
                    return candidatesResult.ToHttpResult();
                }

                var enriched = new List<StuckCardDto>(take);
                foreach (var candidate in candidatesResult.Value!)
                {
                    var stageResult = await mediator
                        .Send(new GetCurrentCardStageQuery(candidate.CardId), ct)
                        .ConfigureAwait(false);

                    var stage = stageResult.IsSuccess ? stageResult.Value : null;
                    if (stage is int s && s > maxStage)
                    {
                        // "Stuck" implies the user is still struggling at an
                        // early stage; if they're already past it, the bad
                        // run is historical and we don't surface it.
                        continue;
                    }

                    var cardResult = await mediator
                        .Send(new GetCardByIdQuery(user.Id, candidate.CardId, IncludeDeleted: false), ct)
                        .ConfigureAwait(false);
                    if (cardResult.IsFailure)
                    {
                        continue;
                    }

                    enriched.Add(new StuckCardDto(
                        CardId: candidate.CardId,
                        Title: cardResult.Value!.Title,
                        ConsecutiveForgotCount: candidate.ConsecutiveForgotCount,
                        LastReviewedAt: candidate.LastReviewedAt,
                        CurrentStage: stage));

                    if (enriched.Count >= take) break;
                }

                return Result<IReadOnlyList<StuckCardDto>>
                    .Success(enriched)
                    .ToHttpResult();
            });

        group.MapGet("/tag-averages", async (
                HttpContext ctx,
                IMediator mediator,
                CancellationToken ct,
                [FromQuery] int take = 10,
                [FromQuery] int minReviews = 3) =>
            {
                var user = ctx.GetCurrentUser();

                // Pull all cards with tags. PageSize=100 hard-cap on the
                // backend — for users with >100 cards we'd need pagination
                // here, but at that scale the dashboard is the wrong place
                // for full-library aggregation anyway.
                var pageResult = await mediator
                    .Send(new ListCardsQuery(user.Id, null, null, Page: 1, PageSize: 100), ct)
                    .ConfigureAwait(false);
                if (pageResult.IsFailure)
                {
                    return pageResult.ToHttpResult();
                }

                var cards = pageResult.Value!.Items;
                if (cards.Count == 0)
                {
                    return Result<IReadOnlyList<TagAverageDto>>
                        .Success(Array.Empty<TagAverageDto>())
                        .ToHttpResult();
                }

                var statsResult = await mediator
                    .Send(new GetCardGradeStatsQuery(user.Id, cards.Select(c => c.Id).ToList()), ct)
                    .ConfigureAwait(false);
                if (statsResult.IsFailure)
                {
                    return statsResult.ToHttpResult();
                }
                var statsById = statsResult.Value!.ToDictionary(s => s.CardId);

                var perTag = new Dictionary<string, (double SumScore, int Reviews, int Cards)>();
                foreach (var card in cards)
                {
                    if (!statsById.TryGetValue(card.Id, out var stats)) continue;
                    var score = stats.AvgAiScore ?? stats.AvgRating;
                    if (score is null) continue;

                    foreach (var tag in card.Tags)
                    {
                        perTag.TryGetValue(tag, out var acc);
                        perTag[tag] = (
                            acc.SumScore + score.Value,
                            acc.Reviews + stats.ReviewCount,
                            acc.Cards + 1);
                    }
                }

                var ranked = perTag
                    .Where(kv => kv.Value.Reviews >= minReviews)
                    .Select(kv => new TagAverageDto(
                        Tag: kv.Key,
                        CardCount: kv.Value.Cards,
                        ReviewCount: kv.Value.Reviews,
                        AvgScore: kv.Value.SumScore / kv.Value.Cards))
                    .OrderBy(t => t.AvgScore)
                    .Take(take)
                    .ToList();

                return Result<IReadOnlyList<TagAverageDto>>
                    .Success(ranked)
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
                        req.Note,
                        req.AnswerText,
                        req.AiScore,
                        req.AiFeedback,
                        req.AutoGraded), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        group.MapPost("/{id:guid}/grade-answer", async (
                HttpContext ctx,
                Guid id,
                [FromBody] GradeAnswerRequest req,
                IMediator mediator,
                IAnswerGrader grader,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();

                if (string.IsNullOrWhiteSpace(req.UserAnswer))
                {
                    return Result<GradingResult>.Failure(Error.Validation(
                        "reviews.answer_empty", "Answer cannot be empty.")).ToHttpResult();
                }

                var cardResult = await mediator
                    .Send(new GetCardByIdQuery(user.Id, id, IncludeDeleted: false), ct)
                    .ConfigureAwait(false);
                if (cardResult.IsFailure)
                {
                    return cardResult.ToHttpResult();
                }

                var card = cardResult.Value!;
                if (card.Type != CardType.Question)
                {
                    return Result<GradingResult>.Failure(Error.Validation(
                        "reviews.card_not_question",
                        "Only Question cards can be auto-graded.")).ToHttpResult();
                }

                var gradeResult = await grader
                    .GradeAsync(new GradingRequest(user.Id, card.Title, card.Body, req.UserAnswer), ct)
                    .ConfigureAwait(false);
                return gradeResult.ToHttpResult();
            });

        return app;
    }
}
