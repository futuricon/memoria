using global::Hangfire;

using Memoria.Api.Authentication;
using Memoria.Api.Configuration;
using Memoria.Api.Endpoints;
using Memoria.Api.Hangfire;
using Memoria.Api.Middleware;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.Api;

/// <summary>
/// Регистрирует все компоненты Api-слоя (auth, CORS, OpenAPI, rate-limiting,
/// health-checks, exception handler) и предоставляет extension для маппинга
/// всех endpoint-групп одним вызовом.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApiModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Behind nginx (TLS terminates there, app listens on http://127.0.0.1:8080)
        // we must honor X-Forwarded-Proto/Host so Request.Scheme is "https" — OAuth
        // redirect_uri and the dashboard cookie depend on it. The app is only
        // reachable via the loopback proxy, so trust forwarded headers from anyone.
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                 | ForwardedHeaders.XForwardedProto
                                 | ForwardedHeaders.XForwardedHost;
            o.KnownNetworks.Clear();
            o.KnownProxies.Clear();
        });

        services.AddJwtBearerAuthentication(configuration);
        services.AddOAuthAuthentication(configuration);

        services.AddOptions<OAuthOptions>()
            .Bind(configuration.GetSection(OAuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<HangfireDashboardOptions>()
            .Bind(configuration.GetSection(HangfireDashboardOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<HangfireDashboardAuthorizationFilter>();

        services.AddCorsPolicy(configuration);
        services.AddOpenApiServices();
        services.AddRateLimiting();
        services.AddHealthChecksConfig(configuration);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddSingleton<TelegramWidgetValidator>();

        return services;
    }

    /// <summary>
    /// Маппит <c>/jobs</c> Hangfire-dashboard и /jobs/login-эндпоинты. Middleware
    /// challenge регистрируется отдельно через <see cref="UseHangfireChallenge"/>.
    /// </summary>
    public static WebApplication MapHangfireDashboard(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var filter = app.Services.GetRequiredService<HangfireDashboardAuthorizationFilter>();
        app.UseHangfireDashboard("/jobs", new DashboardOptions
        {
            Authorization = new[] { filter },
            DashboardTitle = "Memoria · Hangfire",
            AppPath = "/",
        });
        app.MapHangfireDashboardLogin();

        return app;
    }

    /// <summary>
    /// Регистрирует <see cref="HangfireChallengeMiddleware"/>. Должен быть
    /// вставлен ДО <see cref="MapHangfireDashboard"/>.
    /// </summary>
    public static WebApplication UseHangfireChallenge(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseMiddleware<HangfireChallengeMiddleware>();
        return app;
    }

    /// <summary>
    /// Настраивает middleware-pipeline Api-слоя в правильном порядке:
    /// <c>OperationContext</c> (раньше всего — чтобы CorrelationId был на всех
    /// последующих лог-строках, включая ExceptionHandler) →
    /// <c>UseExceptionHandler</c> → CORS → Authentication → Authorization →
    /// RateLimiter → Swagger UI (только Development).
    /// Должен вызываться до <see cref="MapApiEndpoints"/>.
    /// </summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // First: rewrite scheme/host from the nginx proxy headers, before anything
        // (auth, logging, OAuth redirect building) reads them.
        app.UseForwardedHeaders();
        app.UseMiddleware<OperationContextMiddleware>();
        app.UseExceptionHandler();
        app.UseCors(CorsConfiguration.PolicyName);
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.UseOpenApiUi();

        return app;
    }

    /// <summary>
    /// Маппит все Api-эндпоинты. Вызывать после <see cref="UseApiPipeline"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthEndpoints();

        app.MapBotCodeAndRefreshEndpoints();
        app.MapEmailAuthEndpoints();
        app.MapTelegramWidgetEndpoint();

        app.MapUsersEndpoints();
        app.MapCardsEndpoints();
        app.MapCardsTrashEndpoints();
        app.MapCardsActivityEndpoints();
        app.MapTagsEndpoints();

        return app;
    }
}
