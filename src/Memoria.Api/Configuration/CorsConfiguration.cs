using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Memoria.Api.Configuration;

/// <summary>
/// CORS-конфигурация. Никаких <c>AllowAnyOrigin</c> — список origins грузится из
/// секции <c>Cors:AllowedOrigins</c>. Dev defaults для Vite/CRA задаются в
/// <c>appsettings.json</c> хоста.
/// </summary>
internal static class CorsConfiguration
{
    public const string PolicyName = "default";

    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var origins = configuration
            .GetSection($"{CorsOptions.SectionName}:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        services.AddCors(o =>
        {
            o.AddPolicy(PolicyName, policy =>
            {
                policy
                    .WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }
}
