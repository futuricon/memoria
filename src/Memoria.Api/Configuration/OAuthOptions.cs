namespace Memoria.Api.Configuration;

/// <summary>
/// OAuth providers for cookie-auth on the Hangfire dashboard. Optional: if
/// ClientId/ClientSecret are empty, the provider is not registered and the
/// <c>/jobs</c> dashboard is unreachable (the rest of the API keeps working).
/// Real values live in user-secrets / environment variables, never in git.
/// </summary>
public sealed class OAuthOptions
{
    public const string SectionName = "OAuth";

    public OAuthProviderOptions Google { get; init; } = new();
    public OAuthProviderOptions GitHub { get; init; } = new();
}

public sealed class OAuthProviderOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
}
