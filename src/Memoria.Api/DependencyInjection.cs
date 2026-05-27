using global::Hangfire;

using Memoria.Api.Authentication;
using Memoria.Api.Configuration;
using Memoria.Api.Endpoints;
using Memoria.Api.Hangfire;
using Memoria.Api.Middleware;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
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
    /// Настраивает middleware-pipeline Api-слоя: глобальный exception-handler,
    /// CORS, аутентификацию, авторизацию, rate-limiter и Swagger UI (только
    /// в Development). Должен вызываться до <see cref="MapApiEndpoints"/>.
    /// </summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

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
