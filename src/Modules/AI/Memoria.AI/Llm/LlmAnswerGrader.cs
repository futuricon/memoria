using System.Text.Json;
using System.Text.Json.Nodes;

using Memoria.AI.Contracts.Abstractions;
using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Options;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Options;

namespace Memoria.AI.Llm;

/// <summary>
/// Provider-agnostic answer grader: builds the prompt + JSON schema and
/// delegates the structured call to whichever <see cref="ILlmToolClient"/> is
/// configured (Claude / DeepSeek).
/// </summary>
internal sealed class LlmAnswerGrader : IAnswerGrader
{
    private const string ToolName = "submit_grade";

    private const string System =
        "You are a strict but fair grader for spaced-repetition flashcards. " +
        "You are given a question, the reference answer, and a student's answer. " +
        "Score 0-100 by how completely and correctly the student's answer matches " +
        "the reference answer's meaning — ignore wording, phrasing and formatting " +
        "differences, judge substance. An empty or off-topic answer scores near 0. " +
        "Write concise feedback (1-3 sentences) in the same language as the question.";

    private readonly ILlmToolClient _client;
    private readonly AiOptions _options;

    public LlmAnswerGrader(ILlmToolClient client, IOptions<AiOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _client = client;
        _options = options.Value;
    }

    public async Task<Result<GradingResult>> GradeAsync(GradingRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user =
            "Question:\n" + request.Question + "\n\n" +
            "Reference answer:\n" + request.ReferenceBody + "\n\n" +
            "Student answer:\n" + request.UserAnswer;

        var result = await _client.InvokeToolAsync(
            _options.GradingModel,
            System,
            user,
            ToolName,
            "Submit the grade for the student's answer.",
            BuildSchema(),
            ct).ConfigureAwait(false);

        return result.IsFailure
            ? Result<GradingResult>.Failure(result.Error!)
            : Parse(result.Value);
    }

    private static JsonObject BuildSchema() => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["score"] = new JsonObject
            {
                ["type"] = "integer",
                ["minimum"] = 0,
                ["maximum"] = 100,
                ["description"] = "How well the student's answer matches the reference, 0-100.",
            },
            ["verdict"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray { "Incorrect", "Partial", "Correct" },
                ["description"] = "Coarse label: Incorrect (<40), Partial (40-84), Correct (>=85).",
            },
            ["feedback"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Concise feedback for the student, 1-3 sentences.",
            },
        },
        ["required"] = new JsonArray { "score", "verdict", "feedback" },
    };

    private static Result<GradingResult> Parse(JsonElement input)
    {
        if (!input.TryGetProperty("score", out var scoreEl)
            || scoreEl.ValueKind != JsonValueKind.Number
            || !scoreEl.TryGetInt32(out var score))
        {
            return Result<GradingResult>.Failure(Error.Unexpected(
                "ai.parse_failed", "AI grade was missing a numeric score."));
        }

        score = Math.Clamp(score, 0, 100);

        var feedback = input.TryGetProperty("feedback", out var fb) && fb.ValueKind == JsonValueKind.String
            ? fb.GetString() ?? string.Empty
            : string.Empty;

        var verdict = ParseVerdict(input, score);

        return Result<GradingResult>.Success(new GradingResult(score, verdict, feedback));
    }

    private static GradingVerdict ParseVerdict(JsonElement input, int score)
    {
        if (input.TryGetProperty("verdict", out var v)
            && v.ValueKind == JsonValueKind.String
            && Enum.TryParse<GradingVerdict>(v.GetString(), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        // Fall back to a score-derived verdict if the model omitted/garbled it.
        return score >= 85 ? GradingVerdict.Correct
            : score >= 40 ? GradingVerdict.Partial
            : GradingVerdict.Incorrect;
    }
}
