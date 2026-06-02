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
    public const string FakeModel = "fake-model";
    public const int FakeInputTokens = 11;
    public const int FakeOutputTokens = 7;

    private readonly Result<LlmToolInvocation> _result;

    private FakeLlmToolClient(Result<LlmToolInvocation> result) => _result = result;

    public static FakeLlmToolClient Returning(JsonObject toolInput)
    {
        using var doc = JsonDocument.Parse(toolInput.ToJsonString());
        var invocation = new LlmToolInvocation(
            doc.RootElement.Clone(),
            FakeInputTokens,
            FakeOutputTokens,
            FakeModel);
        return new FakeLlmToolClient(Result<LlmToolInvocation>.Success(invocation));
    }

    public static FakeLlmToolClient Failing(Error error) =>
        new(Result<LlmToolInvocation>.Failure(error));

    public Task<Result<LlmToolInvocation>> InvokeToolAsync(
        string model,
        string system,
        string userMessage,
        string toolName,
        string toolDescription,
        JsonNode inputSchema,
        CancellationToken ct) => Task.FromResult(_result);
}
