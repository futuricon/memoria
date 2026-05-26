using MediatR;

using Memoria.Api.Authentication;
using Memoria.Api.Configuration;
using Memoria.Api.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Contracts.Queries;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Memoria.Api.Endpoints;

public sealed record UpdateMeRequest(
    string TimeZoneId,
    TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd);

internal static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app
            .MapGroup("/api/v1/users/me")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingConfiguration.DefaultPolicy);

        group.MapGet("/", async (
                HttpContext ctx,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new GetCurrentUserQuery(user.Id), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        group.MapPatch("/", async (
                HttpContext ctx,
                [FromBody] UpdateMeRequest req,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new UpdateUserPreferencesCommand(
                        user.Id,
                        req.TimeZoneId,
                        req.QuietHoursStart,
                        req.QuietHoursEnd), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResultNoContent();
            });

        group.MapGet("/identities", async (
                HttpContext ctx,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new GetUserIdentitiesQuery(user.Id), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        return app;
    }
}
