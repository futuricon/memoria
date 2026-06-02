namespace Memoria.AI.Features.Stats;

/// <summary>
/// Maps a stored model name (e.g. "claude-sonnet-4-6", "deepseek-chat") to
/// a coarse provider label for dashboard stacking. Unknown prefixes fall
/// back to "other" — defensive against future providers.
/// </summary>
internal static class AiProviderInference
{
    public const string Claude = "claude";
    public const string DeepSeek = "deepseek";
    public const string Other = "other";

    public static string FromModel(string model)
    {
        if (string.IsNullOrEmpty(model)) return Other;
        if (model.StartsWith("claude", StringComparison.OrdinalIgnoreCase)) return Claude;
        if (model.StartsWith("deepseek", StringComparison.OrdinalIgnoreCase)) return DeepSeek;
        return Other;
    }
}
