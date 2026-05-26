using MediatR;

using Memoria.Api.Configuration;
using Memoria.Api.Results;
using Memoria.Users.Contracts.Commands;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Memoria.Api.Endpoints;

public sealed record EmailStartRequest(Guid? UserId, string Email);
public sealed record EmailConfirmRequest(string Email, string Code);

internal static class AuthEmailEndpoints
{
    public static IEndpointRouteBuilder MapEmailAuthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var group = app
            .MapGroup("/api/v1/auth/email")
            .RequireRateLimiting(RateLimitingConfiguration.AuthPolicy)
            .AllowAnonymous();

        group.MapPost("/start", async (
                [FromBody] EmailStartRequest req,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var result = await mediator
                    .Send(new StartEmailLinkingCommand(req.UserId, req.Email), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResultNoContent();
            });

        group.MapPost("/confirm", async (
                [FromBody] EmailConfirmRequest req,
                IMediator mediator,
                CancellationToken ct) =>
            {
                var result = await mediator
                    .Send(new CompleteEmailLinkingCommand(req.Email, req.Code), ct)
                    .ConfigureAwait(false);
                return result.ToHttpResult();
            });

        return app;
    }
}
