using System.Globalization;

using MediatR;

using Memoria.Api.Authentication;
using Memoria.Api.Configuration;
using Memoria.Api.Results;
using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Queries;
using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Queries;
using Memoria.Shared.Kernel.Results;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Memoria.Api.Endpoints;

public sealed record CreateCardRequest(string Title, string Body, IReadOnlyList<string>? Tags);
public sealed record UpdateCardRequest(string? Title, string? Body, IReadOnlyList<string>? Tags);

internal static class CardsEndpoints
{
    public static IEndpointRouteBuilder MapCardsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app
            .MapGroup("/api/v1/cards")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingConfiguration.DefaultPolicy);

        group.MapGet("/", async (
                HttpContext ctx,
                IMediator mediator,
                CancellationToken ct,
                [FromQuery] string? search = null,
                [FromQuery] string? tags = null,
                [FromQuery] int page = 1,
                [FromQuery] int pageSize = 10) =>
            {
                var user = ctx.GetCurrentUser();
                var tagList = string.IsNullOrWhiteSpace(tags)
                    ? null
                    : tags.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

                var listed = await mediator
                    .Send(new ListCardsQuery(user.Id, search, tagList, page, pageSize), ct)
                    .ConfigureAwait(false);

                if (listed.IsFailure)
                {
                    return listed.ToHttpResult();
                }

                var enriched = await EnrichWithStatsAsync(listed.Value!, user.Id, mediator, ct)
                    .ConfigureAwait(false);
                return Result<PagedResult<CardSummaryDto>>.Success(enriched).ToHttpResult();
            });

        group.MapPost("/", async (
                HttpContext ctx,
                [FromBody] CreateCardRequest req,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new AddCardCommand(
                        user.Id,
                        req.Title,
                        req.Body,
                        req.Tags ?? Array.Empty<string>()), ct)
                    .ConfigureAwait(false);

                return result.ToHttpResult(card => Microsoft.AspNetCore.Http.Results.Created(
                    $"/api/v1/cards/{card.Id.ToString("D", CultureInfo.InvariantCulture)}",
                    card));
            });

        group.MapGet("/{id:guid}", async (
                HttpContext ctx,
                IMediator mediator,
                Guid id,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new GetCardByIdQuery(user.Id, id, IncludeDeleted: false), ct)
                    .ConfigureAwait(false);

                if (result.IsFailure)
                {
                    return result.ToHttpResult();
                }

                var card = result.Value!;
                var statsResult = await mediator
                    .Send(new GetCardGradeStatsQuery(user.Id, new[] { card.Id }), ct)
                    .ConfigureAwait(false);

                var stats = statsResult.IsSuccess && statsResult.Value!.Count > 0
                    ? statsResult.Value![0]
                    : null;

                var enriched = stats is null
                    ? card
                    : card with
                    {
                        ReviewCount = stats.ReviewCount,
                        AvgRating = stats.AvgRating,
                        AvgAiScore = stats.AvgAiScore,
                    };

                return Result<CardDto>.Success(enriched).ToHttpResult();
            });

        group.MapPatch("/{id:guid}", async (
                HttpContext ctx,
                Guid id,
                [FromBody] UpdateCardRequest req,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new UpdateCardCommand(user.Id, id, req.Title, req.Body, req.Tags), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        group.MapDelete("/{id:guid}", async (
                HttpContext ctx,
                IMediator mediator,
                Guid id,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new SoftDeleteCardCommand(user.Id, id), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResultNoContent();
            });

        group.MapPost("/{id:guid}/pause", async (
                HttpContext ctx,
                IMediator mediator,
                Guid id,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new PauseCardCommand(user.Id, id), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResultNoContent();
            });

        group.MapPost("/{id:guid}/unpause", async (
                HttpContext ctx,
                IMediator mediator,
                Guid id,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new UnpauseCardCommand(user.Id, id), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResultNoContent();
            });

        return app;
    }

    private static async Task<PagedResult<CardSummaryDto>> EnrichWithStatsAsync(
        PagedResult<CardSummaryDto> page,
        Guid userId,
        IMediator mediator,
        CancellationToken ct)
    {
        if (page.Items.Count == 0)
        {
            return page;
        }

        var ids = page.Items.Select(c => c.Id).ToList();
        var statsResult = await mediator
            .Send(new GetCardGradeStatsQuery(userId, ids), ct)
            .ConfigureAwait(false);

        if (statsResult.IsFailure)
        {
            return page;
        }

        var byId = statsResult.Value!.ToDictionary(s => s.CardId);
        var enriched = page.Items
            .Select(c => byId.TryGetValue(c.Id, out var s)
                ? c with
                {
                    ReviewCount = s.ReviewCount,
                    AvgRating = s.AvgRating,
                    AvgAiScore = s.AvgAiScore,
                }
                : c)
            .ToList();

        return new PagedResult<CardSummaryDto>(enriched, page.Page, page.PageSize, page.TotalCount);
    }
}
