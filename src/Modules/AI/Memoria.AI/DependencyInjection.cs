using System.Net.Http.Headers;

using Memoria.AI.Claude;
using Memoria.AI.Contracts.Abstractions;
using Memoria.AI.Deepseek;
using Memoria.AI.Llm;
using Memoria.AI.Options;
using Memoria.AI.Persistence;
using Memoria.AI.Pricing;
using Memoria.AI.Quota;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Memoria.AI;

/// <summary>
/// Registers the AI adapters. The active LLM backend is selected by
/// <c>Ai:Provider</c>: the matching typed <see cref="System.Net.Http.HttpClient"/>
/// is bound to <c>ILlmToolClient</c>, and the provider-agnostic
/// <see cref="IAnswerGrader"/> / <see cref="IQuestionCardValidator"/> sit on top.
/// Also registers the <c>ai</c> EF schema used to persist per-user usage rows.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAiModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(AiOptions.SectionName);
        services.AddOptions<AiOptions>().Bind(section);

        var options = section.Get<AiOptions>() ?? new AiOptions();

        switch (options.Provider)
        {
            case AiProvider.DeepSeek:
                services.AddHttpClient<ILlmToolClient, DeepSeekClient>(ConfigureDeepSeek);
                break;
            default:
                services.AddHttpClient<ILlmToolClient, ClaudeClient>(ConfigureClaude);
                break;
        }

        services.AddScoped<IAnswerGrader, LlmAnswerGrader>();
        services.AddScoped<IQuestionCardValidator, LlmQuestionCardValidator>();

        services.AddSingleton<AiModelPricing>();

        services.AddOptions<AiQuotaOptions>()
            .Bind(configuration.GetSection(AiQuotaOptions.SectionName));
        services.AddSingleton<IAiQuotaService, AlwaysAllowAiQuotaService>();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddDbContext<AiDbContext>(o =>
            o.UseNpgsql(connectionString, n => n.MigrationsHistoryTable(
                tableName: "__ef_migrations_history",
                schema: AiDbContext.SchemaName))
             .UseSnakeCaseNamingConvention());

        return services;
    }

    /// <summary>Applies pending migrations for <see cref="AiDbContext"/>.</summary>
    public static async Task MigrateAiModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var db = services.GetRequiredService<AiDbContext>();
        await db.Database.MigrateAsync();
    }

    private static void ConfigureClaude(IServiceProvider sp, HttpClient http)
    {
        var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
        http.BaseAddress = new Uri(
            string.IsNullOrWhiteSpace(options.BaseUrl) ? ClaudeClient.DefaultBaseUrl : options.BaseUrl);
        http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            http.DefaultRequestHeaders.Add("x-api-key", options.ApiKey);
        }
    }

    private static void ConfigureDeepSeek(IServiceProvider sp, HttpClient http)
    {
        var options = sp.GetRequiredService<IOptions<AiOptions>>().Value;
        http.BaseAddress = new Uri(
            string.IsNullOrWhiteSpace(options.BaseUrl) ? DeepSeekClient.DefaultBaseUrl : options.BaseUrl);
        http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }
    }
}
