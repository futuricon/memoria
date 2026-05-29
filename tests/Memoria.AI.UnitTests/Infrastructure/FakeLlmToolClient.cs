using System.Text.Json;
using System.Text.Json.Nodes;

using Memoria.AI.Llm;
using Memoria.Shared.Kernel.Results;

namespace Memoria.AI.UnitTests.Infrastructure;

/// <summary>
/// In-memory <see cref="ILlmToolClient"/> for grader/validator unit tests:
/// returns a preset tool-input object (or a failure) without any HTTP.
/// </summary>
internal sealed class FakeLlmToolClient : ILlmToolClient
{
    private readonly Result<JsonElement> _result;

    private FakeLlmToolClient(Result<JsonElement> result) => _result = result;

    public static FakeLlmToolClient Returning(JsonObject toolInput)
    {
        using var doc = JsonDocument.Parse(toolInput.ToJsonString());
        return new FakeLlmToolClient(Result<JsonElement>.Success(doc.RootElement.Clone()));
    }

    public static FakeLlmToolClient Failing(Error error) =>
        new(Result<JsonElement>.Failure(error));

    public Task<Result<JsonElement>> InvokeToolAsync(
        string model,
        string system,
        string userMessage,
        string toolName,
        string toolDescription,
        JsonNode inputSchema,
        CancellationToken ct) => Task.FromResult(_result);
}
