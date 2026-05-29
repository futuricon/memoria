using System.Text.Json;
using System.Text.Json.Nodes;

using Memoria.AI.Contracts.Abstractions;
using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Options;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Options;

namespace Memoria.AI.Llm;

/// <summary>
/// Provider-agnostic Question-card validator: checks that a card body coherently
/// answers its title, delegating the structured call to the configured
/// <see cref="ILlmToolClient"/>.
/// </summary>
internal sealed class LlmQuestionCardValidator : IQuestionCardValidator
{
    private const string ToolName = "submit_validation";

    private const string System =
        "You validate spaced-repetition flashcards before they are saved. " +
        "A Question card has a question (title) and an intended reference answer (body). " +
        "Decide whether the body is a coherent, on-topic answer to the question. " +
        "Set is_coherent=false ONLY when the body clearly does not answer the question, " +
        "is empty, or is nonsensical. Be lenient: partial or terse answers are still coherent. " +
        "When not coherent, give a short reason in the same language as the question.";

    private readonly ILlmToolClient _client;
    private readonly AiOptions _options;

    public LlmQuestionCardValidator(ILlmToolClient client, IOptions<AiOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _client = client;
        _options = options.Value;
    }

    public async Task<Result<QuestionCardValidation>> ValidateAsync(string question, string body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(body);

        var user = "Question:\n" + question + "\n\nReference answer (body):\n" + body;

        var result = await _client.InvokeToolAsync(
            _options.ValidationModel,
            System,
            user,
            ToolName,
            "Submit whether the card body coherently answers the question.",
            BuildSchema(),
            ct).ConfigureAwait(false);

        return result.IsFailure
            ? Result<QuestionCardValidation>.Failure(result.Error!)
            : Parse(result.Value);
    }

    private static JsonObject BuildSchema() => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["is_coherent"] = new JsonObject
            {
                ["type"] = "boolean",
                ["description"] = "True if the body is a coherent, on-topic answer to the question.",
            },
            ["reason"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "If not coherent, a short reason; otherwise an empty string.",
            },
        },
        ["required"] = new JsonArray { "is_coherent", "reason" },
    };

    private static Result<QuestionCardValidation> Parse(JsonElement input)
    {
        if (!input.TryGetProperty("is_coherent", out var coherentEl)
            || coherentEl.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return Result<QuestionCardValidation>.Failure(Error.Unexpected(
                "ai.parse_failed", "AI validation was missing the is_coherent flag."));
        }

        var isCoherent = coherentEl.GetBoolean();

        string? reason = null;
        if (!isCoherent
            && input.TryGetProperty("reason", out var reasonEl)
            && reasonEl.ValueKind == JsonValueKind.String)
        {
            var text = reasonEl.GetString();
            reason = string.IsNullOrWhiteSpace(text) ? null : text;
        }

        return Result<QuestionCardValidation>.Success(new QuestionCardValidation(isCoherent, reason));
    }
}
