using MediatR;

using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Contracts.Queries;
using Memoria.Api.Authentication;
using Memoria.Api.Configuration;
using Memoria.Api.Results;
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
                IAuditLogger audit,
                CancellationToken ct) =>
            {
                var admin = ctx.GetCurrentUser();

                await audit.LogAsync(
                    admin.Id,
                    action: "admin.overview.read",
                    subject: "overview",
                    metadata: null,
                    ct).ConfigureAwait(false);

                // Phase 3 fills this in with KPI widgets composed via parallel
                // MediatR sends. For now return a stub so the SPA can wire the
                // page shell.
                return Microsoft.AspNetCore.Http.Results.Ok(new AdminOverviewDto(0));
            });

        return app;
    }
}
