using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Memoria.AI.Llm;
using Memoria.AI.Options;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace Memoria.AI.Claude;

/// <summary>
/// Thin typed-<see cref="HttpClient"/> wrapper over the Anthropic Messages API.
/// Forces structured output via a single tool and <c>tool_choice</c>, returning
/// the tool's <c>input</c> object along with the token-usage block reported by
/// the provider. All transport and protocol errors map to a failed
/// <see cref="Result{T}"/> — callers never see exceptions for provider
/// unavailability.
/// </summary>
internal sealed class ClaudeClient : ILlmToolClient
{
    public const string DefaultBaseUrl = "https://api.anthropic.com";
    public const string DefaultModel = "claude-sonnet-4-6";

    private static readonly Uri MessagesEndpoint = new("v1/messages", UriKind.Relative);
    private const int MaxLoggedBody = 500;

    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly ILogger<ClaudeClient> _logger;

    public ClaudeClient(HttpClient http, IOptions<AiOptions> options, ILogger<ClaudeClient> logger)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<LlmToolInvocation>> InvokeToolAsync(
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
            _logger.LogWarning("Claude API key is not configured; skipping call to tool {Tool}", toolName);
            return Result<LlmToolInvocation>.Failure(Error.Unexpected(
                "ai.not_configured", "AI provider is not configured."));
        }

        var resolvedModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;

        var body = new JsonObject
        {
            ["model"] = resolvedModel,
            ["max_tokens"] = _options.MaxTokens,
            ["temperature"] = 0,
            ["system"] = system,
            ["messages"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = userMessage,
                },
            },
            ["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = toolName,
                    ["description"] = toolDescription,
                    ["input_schema"] = inputSchema,
                },
            },
            ["tool_choice"] = new JsonObject
            {
                ["type"] = "tool",
                ["name"] = toolName,
            },
        };

        using var content = new StringContent(body.ToJsonString(), Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(MessagesEndpoint, content, ct).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Claude request failed (network)");
            return Result<LlmToolInvocation>.Failure(Error.Unexpected("ai.request_failed", "AI request failed."));
        }
        catch (TaskCanceledException ex)
        {
            if (ct.IsCancellationRequested)
            {
                throw;
            }

            _logger.LogWarning(ex, "Claude request timed out");
            return Result<LlmToolInvocation>.Failure(Error.Unexpected("ai.timeout", "AI request timed out."));
        }

        using (response)
        {
            var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Claude returned {Status}: {Body}", (int)response.StatusCode, Truncate(payload));
                return Result<LlmToolInvocation>.Failure(Error.Unexpected(
                    "ai.bad_status", "AI provider returned an error status."));
            }

            return ExtractInvocation(payload, toolName, resolvedModel);
        }
    }

    private Result<LlmToolInvocation> ExtractInvocation(string payload, string toolName, string resolvedModel)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("content", out var contentArray)
                || contentArray.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Claude response has no content array: {Body}", Truncate(payload));
                return Result<LlmToolInvocation>.Failure(Error.Unexpected(
                    "ai.no_content", "AI response had no content."));
            }

            JsonElement? toolInput = null;
            foreach (var block in contentArray.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type)
                    && string.Equals(type.GetString(), "tool_use", StringComparison.Ordinal)
                    && block.TryGetProperty("input", out var input))
                {
                    // Clone so the element survives disposal of the JsonDocument.
                    toolInput = input.Clone();
                    break;
                }
            }

            if (toolInput is null)
            {
                _logger.LogWarning(
                    "Claude response had no tool_use block for tool {Tool}: {Body}", toolName, Truncate(payload));
                return Result<LlmToolInvocation>.Failure(Error.Unexpected(
                    "ai.no_tool_use", "AI did not return structured output."));
            }

            // Anthropic returns "usage": { "input_tokens": N, "output_tokens": M }.
            // If the block is missing or malformed we still succeed with zeros so
            // grading isn't broken by a wire-format change — the dashboard will
            // just show 0 for those calls, easy to spot.
            var (inputTokens, outputTokens) = ParseUsage(doc.RootElement);
            return Result<LlmToolInvocation>.Success(
                new LlmToolInvocation(toolInput.Value, inputTokens, outputTokens, resolvedModel));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse Claude response");
            return Result<LlmToolInvocation>.Failure(Error.Unexpected(
                "ai.parse_failed", "Failed to parse AI response."));
        }
    }

    private static (int Input, int Output) ParseUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage)
            || usage.ValueKind != JsonValueKind.Object)
        {
            return (0, 0);
        }

        var input = usage.TryGetProperty("input_tokens", out var i) && i.TryGetInt32(out var iv) ? iv : 0;
        var output = usage.TryGetProperty("output_tokens", out var o) && o.TryGetInt32(out var ov) ? ov : 0;
        return (input, output);
    }

    private static string Truncate(string s) =>
        s.Length <= MaxLoggedBody ? s : s[..MaxLoggedBody];
}
