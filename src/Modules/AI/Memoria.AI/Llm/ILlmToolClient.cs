using System.Text.Json.Nodes;

using Memoria.Shared.Kernel.Results;

namespace Memoria.AI.Llm;

/// <summary>
/// Provider-agnostic seam over an LLM that returns structured output by forcing
/// a single tool/function call. Implemented per backend (Anthropic, DeepSeek).
/// Returns the tool's input plus usage metadata in an
/// <see cref="LlmToolInvocation"/>, or a failed <see cref="Result{T}"/> for any
/// transport/protocol error (callers fail-open).
/// </summary>
internal interface ILlmToolClient
{
    Task<Result<LlmToolInvocation>> InvokeToolAsync(
        string model,
        string system,
        string userMessage,
        string toolName,
        string toolDescription,
        JsonNode inputSchema,
        CancellationToken ct);
}
