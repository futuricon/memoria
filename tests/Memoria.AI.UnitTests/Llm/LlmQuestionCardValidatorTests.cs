using System.Text.Json.Nodes;

using FluentAssertions;

using Memoria.AI.Llm;
using Memoria.AI.Options;
using Memoria.AI.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

namespace Memoria.AI.UnitTests.Llm;

public sealed class LlmQuestionCardValidatorTests
{
    private static LlmQuestionCardValidator CreateSut(FakeLlmToolClient client) =>
        new(client, Microsoft.Extensions.Options.Options.Create(new AiOptions()));

    [Fact]
    public async Task ValidateAsyncCoherentReturnsNullReason()
    {
        var client = FakeLlmToolClient.Returning(new JsonObject
        {
            ["is_coherent"] = true,
            ["reason"] = string.Empty,
        });

        var result = await CreateSut(client).ValidateAsync("Q?", "A", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsCoherent.Should().BeTrue();
        result.Value.Reason.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsyncIncoherentReturnsReason()
    {
        var client = FakeLlmToolClient.Returning(new JsonObject
        {
            ["is_coherent"] = false,
            ["reason"] = "Body is off-topic.",
        });

        var result = await CreateSut(client).ValidateAsync("Q?", "unrelated", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsCoherent.Should().BeFalse();
        result.Value.Reason.Should().Be("Body is off-topic.");
    }

    [Fact]
    public async Task ValidateAsyncWithMissingFlagReturnsParseFailure()
    {
        var client = FakeLlmToolClient.Returning(new JsonObject { ["reason"] = "no flag" });

        var result = await CreateSut(client).ValidateAsync("Q?", "A", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ai.parse_failed");
    }

    [Fact]
    public async Task ValidateAsyncPropagatesClientFailure()
    {
        var client = FakeLlmToolClient.Failing(Error.Unexpected("ai.bad_status", "500"));

        var result = await CreateSut(client).ValidateAsync("Q?", "A", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ai.bad_status");
    }
}
