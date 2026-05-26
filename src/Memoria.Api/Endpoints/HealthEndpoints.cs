using System.Text.Json;

using Hangfire;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Memoria.Api.Endpoints;

/// <summary>
/// Liveness/readiness endpoints. <c>/healthz</c> отвечает только за процесс,
/// <c>/readyz</c> агрегирует Postgres + Hangfire-storage и возвращает 503
/// при любом failure, чтобы балансировщик/orchestrator вывел инстанс из ротации.
/// </summary>
internal static class HealthEndpoints
{
    private const string ReadinessTag = "readiness";

    public static IServiceCollection AddHealthChecksConfig(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services
            .AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgres", tags: new[] { ReadinessTag })
            .AddCheck("hangfire", () =>
            {
                try
                {
                    _ = JobStorage.Current.GetMonitoringApi();
                    return HealthCheckResult.Healthy();
                }
                catch (Exception ex)
                {
                    return HealthCheckResult.Unhealthy(exception: ex);
                }
            }, tags: new[] { ReadinessTag });

        return services;
    }

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/healthz", () => Microsoft.AspNetCore.Http.Results.Ok(new { status = "alive" }))
            .AllowAnonymous();

        app.MapHealthChecks("/readyz", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadinessTag),
            ResponseWriter = WriteJsonResponseAsync,
        }).AllowAnonymous();

        return app;
    }

    private static Task WriteJsonResponseAsync(HttpContext ctx, HealthReport report)
    {
        ctx.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                error = e.Value.Exception?.Message,
            }),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
        };
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
