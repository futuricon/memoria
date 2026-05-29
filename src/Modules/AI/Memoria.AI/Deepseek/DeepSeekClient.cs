using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Memoria.AI.Llm;
using Memoria.AI.Options;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Memoria.AI.Deepseek;

/// <summary>
/// Typed-<see cref="HttpClient"/> wrapper over DeepSeek's OpenAI-compatible
/// Chat Completions API. Forces structured output via a single function and
/// <c>tool_choice</c>; the function arguments come back as a JSON-encoded string
/// inside <c>choices[].message.tool_calls[]</c>, which we parse into a
/// <see cref="JsonElement"/>. Transport/protocol errors map to a failed
/// <see cref="Result{T}"/> (callers fail-open).
/// </summary>
internal sealed class DeepSeekClient : ILlmToolClient
{
    public const string DefaultBaseUrl = "https://api.deepseek.com";

    // Function calling is supported on deepseek-chat (V3). deepseek-reasoner (R1)
    // does NOT support tools — don't default to it.
    public const string DefaultModel = "deepseek-chat";

    private static readonly Uri CompletionsEndpoint = new("chat/completions", UriKind.Relative);
    private const int MaxLoggedBody = 500;

    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly ILogger<DeepSeekClient> _logger;

    public DeepSeekClient(HttpClient http, IOptions<AiOptions> options, ILogger<DeepSeekClient> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<JsonElement>> InvokeToolAsync(
        string model,
        string system,
        string userMessage,
        string toolName,
        string toolDescription,
        JsonNode inputSchema,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(inputSchema);

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("DeepSeek API key is not configured; skipping call to tool {Tool}", toolName);
            return Result<JsonElement>.Failure(Error.Unexpected(
                "ai.not_configured", "AI provider is not configured."));
        }

        var body = new JsonObject
        {
            ["model"] = string.IsNullOrWhiteSpace(model) ? DefaultModel : model,
            ["max_tokens"] = _options.MaxTokens,
            ["temperature"] = 0,
            ["messages"] = new JsonArray
            {
                new JsonObject { ["role"] = "system", ["content"] = system },
                new JsonObject { ["role"] = "user", ["content"] = userMessage },
            },
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = toolName,
                        ["description"] = toolDescription,
                        ["parameters"] = inputSchema,
                    },
                },
            },
            ["tool_choice"] = new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = toolName },
            },
        };

        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(CompletionsEndpoint, content, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "DeepSeek request failed (network)");
            return Result<JsonElement>.Failure(Error.Unexpected("ai.request_failed", "AI request failed."));
        }
        catch (TaskCanceledException ex)
        {
            if (ct.IsCancellationRequested)
            {
                throw;
            }

            _logger.LogWarning(ex, "DeepSeek request timed out");
            return Result<JsonElement>.Failure(Error.Unexpected("ai.timeout", "AI request timed out."));
        }

        using (response)
        {
            var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "DeepSeek returned {Status}: {Body}", (int)response.StatusCode, Truncate(payload));
                return Result<JsonElement>.Failure(Error.Unexpected(
                    "ai.bad_status", "AI provider returned an error status."));
            }

            return ExtractToolArguments(payload, toolName);
        }
    }

    private Result<JsonElement> ExtractToolArguments(string payload, string toolName)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);

            if (doc.RootElement.TryGetProperty("choices", out var choices)
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message");
                if (message.TryGetProperty("tool_calls", out var toolCalls)
                    && toolCalls.ValueKind == JsonValueKind.Array
                    && toolCalls.GetArrayLength() > 0)
                {
                    var arguments = toolCalls[0].GetProperty("function").GetProperty("arguments").GetString();
                    if (!string.IsNullOrWhiteSpace(arguments))
                    {
                        using var argsDoc = JsonDocument.Parse(arguments);
                        return Result<JsonElement>.Success(argsDoc.RootElement.Clone());
                    }
                }
            }

            _logger.LogWarning(
                "DeepSeek response had no tool_call for tool {Tool}: {Body}", toolName, Truncate(payload));
            return Result<JsonElement>.Failure(Error.Unexpected(
                "ai.no_tool_use", "AI did not return structured output."));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse DeepSeek response");
            return Result<JsonElement>.Failure(Error.Unexpected(
                "ai.parse_failed", "Failed to parse AI response."));
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "DeepSeek response missing expected fields");
            return Result<JsonElement>.Failure(Error.Unexpected(
                "ai.parse_failed", "Failed to parse AI response."));
        }
    }

    private static string Truncate(string s) =>
        s.Length <= MaxLoggedBody ? s : s[..MaxLoggedBody];
}
