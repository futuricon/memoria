using System.ComponentModel.DataAnnotations;

namespace Memoria.Api.Configuration;

/// <summary>
/// OAuth-провайдеры для cookie-auth Hangfire dashboard. Реальные ClientId/ClientSecret
/// держим в user-secrets / переменных окружения.
/// </summary>
public sealed class OAuthOptions
{
    public const string SectionName = "OAuth";

    [Required]
    public OAuthProviderOptions Google { get; init; } = new();

    [Required]
    public OAuthProviderOptions GitHub { get; init; } = new();
}

public sealed class OAuthProviderOptions
{
    [Required]
    public string ClientId { get; init; } = string.Empty;

    [Required]
    public string ClientSecret { get; init; } = string.Empty;
}
