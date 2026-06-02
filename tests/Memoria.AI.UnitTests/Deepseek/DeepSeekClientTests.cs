using System.Net;
using System.Text.Json.Nodes;

using FluentAssertions;

using Memoria.AI.Deepseek;
using Memoria.AI.Llm;
using Memoria.AI.Options;
using Memoria.AI.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Logging.Abstractions;

namespace Memoria.AI.UnitTests.Deepseek;

public sealed class DeepSeekClientTests
{
    private static AiOptions Options(string apiKey = "test-key") => new()
    {
        Provider = AiProvider.DeepSeek,
        ApiKey = apiKey,
        BaseUrl = DeepSeekClient.DefaultBaseUrl,
        MaxTokens = 256,
        TimeoutSeconds = 30,
    };

    private static DeepSeekClient CreateSut(StubHttpMessageHandler handler, AiOptions? options = null)
    {
        var opts = options ?? Options();
        var http = new HttpClient(handler) { BaseAddress = new Uri(opts.BaseUrl) };
        return new DeepSeekClient(http, Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<DeepSeekClient>.Instance);
    }

    private static JsonObject Schema() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject { ["ok"] = new JsonObject { ["type"] = "boolean" } },
    };

    private static Task<Result<LlmToolInvocation>> Invoke(DeepSeekClient sut, string model = "deepseek-chat") =>
        sut.InvokeToolAsync(model, "system", "user", "submit", "desc", Schema(), CancellationToken.None);

    [Fact]
    public async Task InvokeToolAsyncParsesFunctionArgumentsString()
    {
        var args = new JsonObject { ["score"] = 73, ["verdict"] = "Partial" };
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            StubHttpMessageHandler.ToolCallResponse("submit", args));
        var sut = CreateSut(handler);

        var result = await Invoke(sut);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Input.GetProperty("score").GetInt32().Should().Be(73);
        result.Value.Input.GetProperty("verdict").GetString().Should().Be("Partial");
    }

    [Fact]
    public async Task InvokeToolAsyncExtractsUsageTokensFromResponse()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            StubHttpMessageHandler.ToolCallResponse(
                "submit", new JsonObject { ["ok"] = true },
                promptTokens: 200, completionTokens: 50));
        var sut = CreateSut(handler);

        var result = await Invoke(sut, model: "deepseek-chat");

        result.IsSuccess.Should().BeTrue();
        result.Value!.InputTokens.Should().Be(200);
        result.Value.OutputTokens.Should().Be(50);
        result.Value.Model.Should().Be("deepseek-chat");
    }

    [Fact]
    public async Task InvokeToolAsyncWithMissingUsageBlockReturnsZeroTokens()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            StubHttpMessageHandler.ToolCallResponse("submit", new JsonObject { ["ok"] = true }));
        var sut = CreateSut(handler);

        var result = await Invoke(sut);

        result.IsSuccess.Should().BeTrue();
        result.Value!.InputTokens.Should().Be(0);
        result.Value.OutputTokens.Should().Be(0);
    }

    [Fact]
    public async Task InvokeToolAsyncFallsBackToDefaultModelWhenModelEmpty()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            StubHttpMessageHandler.ToolCallResponse("submit", new JsonObject { ["ok"] = true }));
        var sut = CreateSut(handler);

        var result = await Invoke(sut, model: "");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Model.Should().Be(DeepSeekClient.DefaultModel);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task InvokeToolAsyncOnErrorStatusReturnsFailure(HttpStatusCode status)
    {
        var handler = new StubHttpMessageHandler(status, "{\"error\":\"x\"}");
        var sut = CreateSut(handler);

        var result = await Invoke(sut);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ai.bad_status");
    }

    [Fact]
    public async Task InvokeToolAsyncWithNoToolCallReturnsFailure()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            StubHttpMessageHandler.ChatTextResponse("sorry, no function"));
        var sut = CreateSut(handler);

        var result = await Invoke(sut);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ai.no_tool_use");
    }

    [Fact]
    public async Task InvokeToolAsyncWithoutApiKeyShortCircuits()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            StubHttpMessageHandler.ToolCallResponse("submit", new JsonObject { ["ok"] = true }));
        var sut = CreateSut(handler, Options(apiKey: string.Empty));

        var result = await Invoke(sut);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ai.not_configured");
        handler.LastRequestBody.Should().BeNull();
    }

    [Fact]
    public async Task InvokeToolAsyncSendsOpenAiFunctionToolChoice()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK,
            StubHttpMessageHandler.ToolCallResponse("submit", new JsonObject { ["ok"] = true }));
        var sut = CreateSut(handler);

        await Invoke(sut);

        handler.LastRequestBody.Should().NotBeNull();
        handler.LastRequestBody.Should().Contain("\"temperature\":0");
        handler.LastRequestBody.Should().Contain("\"type\":\"function\"");
        handler.LastRequestBody.Should().Contain("\"tool_choice\"");
    }
}
