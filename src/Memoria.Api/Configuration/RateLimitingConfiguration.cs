using System.IdentityModel.Tokens.Jwt;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.Api.Configuration;

/// <summary>
/// Rate-limit политики. Регистрируются глобально, применяются per-endpoint через
/// <c>.RequireRateLimiting(PolicyName)</c>.
/// <list type="bullet">
///   <item><c>AuthPolicy</c> — фиксированное окно 5 запросов/минуту по IP. На все
///     <c>/api/v1/auth/*</c>.</item>
///   <item><c>DefaultPolicy</c> — token bucket 60 запросов/минуту по UserId (или IP
///     для анонимных). На остальные endpoints.</item>
/// </list>
/// </summary>
internal static class RateLimitingConfiguration
{
    public const string AuthPolicy = "auth";
    public const string DefaultPolicy = "default";

    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            o.AddPolicy(AuthPolicy, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            o.AddPolicy(DefaultPolicy, ctx =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: ctx.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                                  ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 60,
                        TokensPerPeriod = 60,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
        });

        return services;
    }
}
