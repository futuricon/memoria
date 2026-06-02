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

                // Fan out all module-local KPI queries in parallel — each
                // round-trips to its own schema, so there's nothing for them
                // to contend on.
                var signupsTask = mediator.Send(new GetSignupAndLinkCountsQuery(), ct);
                var withCardTask = mediator.Send(new GetUsersWithCardCountQuery(), ct);
                var withReviewTask = mediator.Send(new GetUsersWithReviewCountQuery(), ct);
                var activeTask = mediator.Send(new GetActiveUserCountsQuery(), ct);
                var retentionTask = mediator.Send(new GetRetentionCohortsQuery(), ct);
                var ratingsTask = mediator.Send(new GetGlobalRatingDistributionQuery(), ct);
                var calibrationTask = mediator.Send(new GetAiCalibrationQuery(), ct);
                var skipRateTask = mediator.Send(new GetReminderSkipRateQuery(), ct);
                var spendTask = mediator.Send(new GetAiSpendTotalsQuery(), ct);
                var spendTrendTask = mediator.Send(new GetAiSpendTrendQuery(), ct);
                var topSpendersTask = mediator.Send(new GetTopSpendersQuery(), ct);
                var failureRateTask = mediator.Send(new GetAiFailureRateQuery(), ct);

                await Task.WhenAll(
                    signupsTask, withCardTask, withReviewTask,
                    activeTask, retentionTask, ratingsTask, calibrationTask,
                    skipRateTask, spendTask, spendTrendTask,
                    topSpendersTask, failureRateTask).ConfigureAwait(false);

                // Any partial failure short-circuits with the first error —
                // the admin overview is read-only, so half-data is worse
                // than a clear 4xx/5xx.
                Error? firstError =
                    signupsTask.Result.Error
                    ?? withCardTask.Result.Error
                    ?? withReviewTask.Result.Error
                    ?? activeTask.Result.Error
                    ?? retentionTask.Result.Error
                    ?? ratingsTask.Result.Error
                    ?? calibrationTask.Result.Error
                    ?? skipRateTask.Result.Error
                    ?? spendTask.Result.Error
                    ?? spendTrendTask.Result.Error
                    ?? topSpendersTask.Result.Error
                    ?? failureRateTask.Result.Error;
                if (firstError is not null)
                {
                    return Result<object>.Failure(firstError).ToHttpResult();
                }

                var signups = signupsTask.Result.Value!;
                var active = activeTask.Result.Value!;
                var spend = spendTask.Result.Value!;
                var costPerActive = active.Mau == 0
                    ? 0m
                    : spend.EstimatedCostUsd / active.Mau;

                var payload = new AdminOverviewPayloadDto(
                    ActivationFunnel: new ActivationFunnelDto(
                        Signups: signups.TotalSignups,
                        TelegramLinked: signups.TelegramLinked,
                        HasCard: withCardTask.Result.Value,
                        HasReview: withReviewTask.Result.Value),
                    ActiveUsers: active,
                    Retention: retentionTask.Result.Value!,
                    GlobalRatings: ratingsTask.Result.Value!,
                    AiCalibration: calibrationTask.Result.Value!,
                    ReminderSkipRate: skipRateTask.Result.Value!,
                    AiSpend: spend,
                    AiSpendTrend: spendTrendTask.Result.Value!,
                    TopSpenders: topSpendersTask.Result.Value!,
                    AiFailureRate: failureRateTask.Result.Value!,
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
