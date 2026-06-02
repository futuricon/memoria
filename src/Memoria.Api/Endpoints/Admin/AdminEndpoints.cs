using MediatR;

using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Contracts.Queries;
using Memoria.Api.Authentication;
using Memoria.Api.Configuration;
using Memoria.Api.Results;
using Memoria.Cards.Contracts.Queries;
using Memoria.Reminders.Contracts.Queries;
using Memoria.Reviews.Contracts.Queries;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Abstractions;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Memoria.Api.Endpoints.Admin;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app
            .MapGroup("/api/v1/admin")
            .RequireAuthorization("admin")
            .RequireRateLimiting(RateLimitingConfiguration.DefaultPolicy);

        group.MapGet("/users", async (
                HttpContext ctx,
                IMediator mediator,
                IAuditLogger audit,
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                [FromQuery] string? search,
                [FromQuery] UserSortKey? sort,
                CancellationToken ct) =>
            {
                var admin = ctx.GetCurrentUser();

                var usersResult = await mediator
                    .Send(new GetUsersPageQuery(page ?? 1, pageSize ?? 25, search, sort), ct)
                    .ConfigureAwait(false);

                if (usersResult.IsFailure)
                {
                    return usersResult.ToHttpResult();
                }

                var pageDto = usersResult.Value!;
                var ids = pageDto.Items.Select(u => u.Id).ToArray();

                var totalsResult = await mediator
                    .Send(new GetUsersTokenTotalsQuery(ids), ct)
                    .ConfigureAwait(false);

                var totals = totalsResult.IsSuccess
                    ? totalsResult.Value!
                    : (IReadOnlyDictionary<Guid, AiUsageTotalsDto>)new Dictionary<Guid, AiUsageTotalsDto>();

                var rows = pageDto.Items
                    .Select(u =>
                    {
                        totals.TryGetValue(u.Id, out var t);
                        return new AdminUserRowDto(
                            u.Id,
                            u.DisplayName,
                            u.Email,
                            u.Role,
                            u.CreatedAt,
                            u.LastSeenAt,
                            u.IsBlocked,
                            u.DeletedAt,
                            u.Identities,
                            t?.TotalInputTokens ?? 0,
                            t?.TotalOutputTokens ?? 0,
                            t?.EstimatedCostUsd ?? 0m,
                            t?.LastCallAt,
                            t?.CallCount ?? 0);
                    })
                    .ToList();

                await audit.LogAsync(
                    admin.Id,
                    action: "admin.users.list",
                    subject: "users",
                    metadata: new
                    {
                        page = pageDto.Page,
                        pageSize = pageDto.PageSize,
                        totalCount = pageDto.TotalCount,
                        search,
                        sort = sort?.ToString(),
                    },
                    ct).ConfigureAwait(false);

                return Microsoft.AspNetCore.Http.Results.Ok(new AdminUserPageDto(
                    rows,
                    pageDto.Page,
                    pageDto.PageSize,
                    pageDto.TotalCount));
            });

        group.MapGet("/overview", async (
                HttpContext ctx,
                IMediator mediator,
                IAuditLogger audit,
                CancellationToken ct) =>
            {
                var admin = ctx.GetCurrentUser();

                // Sequential awaits, NOT Task.WhenAll. Several handlers share
                // the same scoped DbContext per request (Users / AI / Reviews
                // each have multiple sends here), and EF Core throws on
                // concurrent operations against one DbContext instance. The
                // admin overview is opened by hand a few times a day — paying
                // ~50ms of serial latency is the right trade for not having
                // to introduce IDbContextFactory across every module.
                var signups = await mediator.Send(new GetSignupAndLinkCountsQuery(), ct).ConfigureAwait(false);
                if (signups.IsFailure) return Result<object>.Failure(signups.Error!).ToHttpResult();

                var withCard = await mediator.Send(new GetUsersWithCardCountQuery(), ct).ConfigureAwait(false);
                if (withCard.IsFailure) return Result<object>.Failure(withCard.Error!).ToHttpResult();

                var withReview = await mediator.Send(new GetUsersWithReviewCountQuery(), ct).ConfigureAwait(false);
                if (withReview.IsFailure) return Result<object>.Failure(withReview.Error!).ToHttpResult();

                var active = await mediator.Send(new GetActiveUserCountsQuery(), ct).ConfigureAwait(false);
                if (active.IsFailure) return Result<object>.Failure(active.Error!).ToHttpResult();

                var retention = await mediator.Send(new GetRetentionCohortsQuery(), ct).ConfigureAwait(false);
                if (retention.IsFailure) return Result<object>.Failure(retention.Error!).ToHttpResult();

                var ratings = await mediator.Send(new GetGlobalRatingDistributionQuery(), ct).ConfigureAwait(false);
                if (ratings.IsFailure) return Result<object>.Failure(ratings.Error!).ToHttpResult();

                var calibration = await mediator.Send(new GetAiCalibrationQuery(), ct).ConfigureAwait(false);
                if (calibration.IsFailure) return Result<object>.Failure(calibration.Error!).ToHttpResult();

                var skipRate = await mediator.Send(new GetReminderSkipRateQuery(), ct).ConfigureAwait(false);
                if (skipRate.IsFailure) return Result<object>.Failure(skipRate.Error!).ToHttpResult();

                var spend = await mediator.Send(new GetAiSpendTotalsQuery(), ct).ConfigureAwait(false);
                if (spend.IsFailure) return Result<object>.Failure(spend.Error!).ToHttpResult();

                var spendTrend = await mediator.Send(new GetAiSpendTrendQuery(), ct).ConfigureAwait(false);
                if (spendTrend.IsFailure) return Result<object>.Failure(spendTrend.Error!).ToHttpResult();

                var topSpenders = await mediator.Send(new GetTopSpendersQuery(), ct).ConfigureAwait(false);
                if (topSpenders.IsFailure) return Result<object>.Failure(topSpenders.Error!).ToHttpResult();

                var failureRate = await mediator.Send(new GetAiFailureRateQuery(), ct).ConfigureAwait(false);
                if (failureRate.IsFailure) return Result<object>.Failure(failureRate.Error!).ToHttpResult();

                var activeCounts = active.Value!;
                var spendTotals = spend.Value!;
                var costPerActive = activeCounts.Mau == 0
                    ? 0m
                    : spendTotals.EstimatedCostUsd / activeCounts.Mau;

                var payload = new AdminOverviewPayloadDto(
                    ActivationFunnel: new ActivationFunnelDto(
                        Signups: signups.Value!.TotalSignups,
                        TelegramLinked: signups.Value.TelegramLinked,
                        HasCard: withCard.Value,
                        HasReview: withReview.Value),
                    ActiveUsers: activeCounts,
                    Retention: retention.Value!,
                    GlobalRatings: ratings.Value!,
                    AiCalibration: calibration.Value!,
                    ReminderSkipRate: skipRate.Value!,
                    AiSpend: spendTotals,
                    AiSpendTrend: spendTrend.Value!,
                    TopSpenders: topSpenders.Value!,
                    AiFailureRate: failureRate.Value!,
                    CostPerActiveUserUsd: costPerActive);

                await audit.LogAsync(
                    admin.Id,
                    action: "admin.overview.read",
                    subject: "overview",
                    metadata: null,
                    ct).ConfigureAwait(false);

                return Microsoft.AspNetCore.Http.Results.Ok(payload);
            });

        return app;
    }
}
