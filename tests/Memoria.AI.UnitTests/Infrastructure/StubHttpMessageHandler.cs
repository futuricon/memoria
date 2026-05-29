using System.Net;
using System.Text.Json.Nodes;

namespace Memoria.AI.UnitTests.Infrastructure;

/// <summary>
/// Returns a single canned response for every request, capturing the last
/// request body so tests can assert on what was sent to Claude.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly string _body;

    public StubHttpMessageHandler(HttpStatusCode status, string body)
    {
        _status = status;
        _body = body;
    }

    public string? LastRequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        return new HttpResponseMessage(_status)
        {
            Content = new StringContent(_body),
        };
    }

    public static string ToolUseResponse(string toolName, JsonObject input)
    {
        var root = new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "tool_use",
                    ["id"] = "toolu_test",
                    ["name"] = toolName,
                    ["input"] = input,
                },
            },
        };
        return root.ToJsonString();
    }

    public static string TextOnlyResponse(string text)
    {
        var root = new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = text,
                },
            },
        };
        return root.ToJsonString();
    }

    /// <summary>OpenAI-compatible (DeepSeek) response: a forced function call
    /// whose <c>arguments</c> is a JSON-encoded string.</summary>
    public static string ToolCallResponse(string toolName, JsonObject arguments)
    {
        var root = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["message"] = new JsonObject
                    {
                        ["tool_calls"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["function"] = new JsonObject
                                {
                                    ["name"] = toolName,
                                    ["arguments"] = arguments.ToJsonString(),
                                },
                            },
                        },
                    },
                },
            },
        };
        return root.ToJsonString();
    }

    /// <summary>OpenAI-compatible response with no tool call (plain content).</summary>
    public static string ChatTextResponse(string text)
    {
        var root = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["message"] = new JsonObject { ["content"] = text },
                },
            },
        };
        return root.ToJsonString();
    }
}
