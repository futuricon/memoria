using System.Text.Json;
using System.Text.Json.Nodes;

using MediatR;

using Memoria.AI.Contracts.Abstractions;
using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Contracts.Events;
using Memoria.AI.Options;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoria.AI.Llm;

/// <summary>
/// Provider-agnostic Question-card validator: checks that a card body coherently
/// answers its title, delegating the structured call to the configured
/// <see cref="ILlmToolClient"/>. Records every call — success or failure —
/// via the <see cref="AiUsageRecorded"/> notification so admin analytics can
/// attribute spend per user.
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
    private readonly IMediator _mediator;
    private readonly TimeProvider _clock;
    private readonly ILogger<LlmQuestionCardValidator> _logger;

    public LlmQuestionCardValidator(
        ILlmToolClient client,
        IOptions<AiOptions> options,
        IMediator mediator,
        TimeProvider clock,
        ILogger<LlmQuestionCardValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _options = options.Value;
        _mediator = mediator;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<QuestionCardValidation>> ValidateAsync(
        QuestionCardValidationRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = "Question:\n" + request.Question + "\n\nReference answer (body):\n" + request.Body;

        var invocation = await _client.InvokeToolAsync(
            _options.ValidationModel,
            System,
            user,
            ToolName,
            "Submit whether the card body coherently answers the question.",
            BuildSchema(),
            ct).ConfigureAwait(false);

        if (invocation.IsFailure)
        {
            await PublishUsageAsync(request.UserId, _options.ValidationModel, 0, 0, isFailure: true, ct)
                .ConfigureAwait(false);
            return Result<QuestionCardValidation>.Failure(invocation.Error!);
        }

        await PublishUsageAsync(
            request.UserId,
            invocation.Value!.Model,
            invocation.Value.InputTokens,
            invocation.Value.OutputTokens,
            isFailure: false,
            ct).ConfigureAwait(false);

        return Parse(invocation.Value.Input);
    }

    private async Task PublishUsageAsync(
        Guid userId,
        string model,
        int inputTokens,
        int outputTokens,
        bool isFailure,
        CancellationToken ct)
    {
        try
        {
            await _mediator.Publish(
                new AiUsageRecorded(
                    userId,
                    AiOperation.QuestionCardValidation,
                    model,
                    inputTokens,
                    outputTokens,
                    isFailure,
                    _clock.GetUtcNow().UtcDateTime),
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish AiUsageRecorded; validation continues");
        }
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
