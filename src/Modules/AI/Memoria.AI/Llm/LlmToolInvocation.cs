using System.Text.Json;

namespace Memoria.AI.Llm;

/// <summary>
/// Successful result of an <see cref="ILlmToolClient"/> call. Combines the
/// tool's structured input (the actual payload the grader / validator
/// consumes) with the wire-level metadata needed for per-user usage
/// accounting: the resolved model name and the provider-reported token
/// counts.
/// </summary>
internal sealed record LlmToolInvocation(
    JsonElement Input,
    int InputTokens,
    int OutputTokens,
    string Model);
