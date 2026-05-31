using MediatR;

using Memoria.Api.Authentication;
using Memoria.Api.Configuration;
using Memoria.Api.Results;
using Memoria.Reminders.Contracts.Commands;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Memoria.Api.Endpoints;

/// <summary>
/// HTTP surface around <c>Memoria.Reminders</c> — used by the SPA's Practice
/// flow to reveal answers and skip individual reminders. The bot uses the
/// same mediator commands via its callback handlers.
/// </summary>
internal static class RemindersEndpoints
{
    public static IEndpointRouteBuilder MapRemindersEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app
            .MapGroup("/api/v1/reminders")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingConfiguration.DefaultPolicy);

        group.MapPost("/{id:guid}/reveal", async (
                HttpContext ctx,
                IMediator mediator,
                Guid id,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new RevealReminderAnswerCommand(id, user.Id), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        group.MapPost("/{id:guid}/skip", async (
                HttpContext ctx,
                IMediator mediator,
                Guid id,
                CancellationToken ct) =>
            {
                var user = ctx.GetCurrentUser();
                var result = await mediator
                    .Send(new SkipReminderCommand(id, user.Id), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResultNoContent();
            });

        return app;
    }
}
