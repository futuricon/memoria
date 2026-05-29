using System.Net.Http.Headers;

using Memoria.AI.Claude;
using Memoria.AI.Contracts.Abstractions;
using Memoria.AI.Deepseek;
using Memoria.AI.Llm;
using Memoria.AI.Options;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Memoria.AI;

/// <summary>
/// Registers the AI adapters. The active LLM backend is selected by
/// <c>Ai:Provider</c>: the matching typed <see cref="System.Net.Http.HttpClient"/>
/// is bound to <c>ILlmToolClient</c>, and the provider-agnostic
/// <see cref="IAnswerGrader"/> / <see cref="IQuestionCardValidator"/> sit on top.
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

        return services;
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
