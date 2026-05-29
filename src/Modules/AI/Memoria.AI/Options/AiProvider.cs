namespace Memoria.AI.Options;

/// <summary>
/// Which LLM backend the AI adapters talk to. Selected via <c>Ai:Provider</c>;
/// only one is active at a time.
/// </summary>
public enum AiProvider
{
    /// <summary>Anthropic Messages API (claude-*).</summary>
    Claude,

    /// <summary>DeepSeek, OpenAI-compatible chat completions (deepseek-chat).</summary>
    DeepSeek,
}
