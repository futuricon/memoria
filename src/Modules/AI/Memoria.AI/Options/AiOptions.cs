namespace Memoria.AI.Options;

/// <summary>
/// Configuration for the AI adapters (section <c>Ai</c>). The <see cref="ApiKey"/>
/// is intentionally NOT required / validated on start: the app must boot without
/// a key (local dev, fail-open). When the key is missing the adapters return a
/// failed <c>Result</c> instead of calling out.
/// <para>
/// <see cref="BaseUrl"/> and the model fields default to empty — when empty the
/// active <see cref="Provider"/> supplies its own default endpoint and model, so
/// switching providers only requires setting <see cref="Provider"/> and
/// <see cref="ApiKey"/>.
/// </para>
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public AiProvider Provider { get; init; } = AiProvider.Claude;
    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string GradingModel { get; init; } = string.Empty;
    public string ValidationModel { get; init; } = string.Empty;
    public int MaxTokens { get; init; } = 1024;
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Per-model billing rates (USD per 1 000 000 tokens), keyed by model name
    /// (matching what <c>AiUsage.Model</c> stores). Unknown models map to 0 —
    /// the dashboard surfaces this as "unpriced", easy to spot. Missing keys
    /// are not validated so list-price drift doesn't crash startup.
    /// </summary>
    public Dictionary<string, AiModelPriceOptions> Pricing { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
