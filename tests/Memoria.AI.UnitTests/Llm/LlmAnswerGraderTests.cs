using System.Text.Json.Nodes;

using FluentAssertions;

using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Llm;
using Memoria.AI.Options;
using Memoria.AI.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

namespace Memoria.AI.UnitTests.Llm;

public sealed class LlmAnswerGraderTests
{
    private static readonly GradingRequest Request =
        new("What does VACUUM do?", "Reclaims dead tuples.", "It frees dead rows.");

    private static LlmAnswerGrader CreateSut(FakeLlmToolClient client) =>
        new(client, Microsoft.Extensions.Options.Options.Create(new AiOptions()));

    [Fact]
    public async Task GradeAsyncParsesScoreVerdictAndFeedback()
    {
        var client = FakeLlmToolClient.Returning(new JsonObject
        {
            ["score"] = 90,
            ["verdict"] = "Correct",
            ["feedback"] = "Good answer.",
        });

        var result = await CreateSut(client).GradeAsync(Request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Score.Should().Be(90);
        result.Value.Verdict.Should().Be(GradingVerdict.Correct);
        result.Value.Feedback.Should().Be("Good answer.");
    }

    [Fact]
    public async Task GradeAsyncClampsOutOfRangeScore()
    {
        var client = FakeLlmToolClient.Returning(new JsonObject
        {
            ["score"] = 150,
            ["verdict"] = "Correct",
            ["feedback"] = "x",
        });

        var result = await CreateSut(client).GradeAsync(Request, CancellationToken.None);

        result.Value!.Score.Should().Be(100);
    }

    [Fact]
    public async Task GradeAsyncDerivesVerdictWhenMissing()
    {
        var client = FakeLlmToolClient.Returning(new JsonObject
        {
            ["score"] = 50,
            ["feedback"] = "partial",
        });

        var result = await CreateSut(client).GradeAsync(Request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Verdict.Should().Be(GradingVerdict.Partial);
    }

    [Fact]
    public async Task GradeAsyncPropagatesClientFailure()
    {
        var client = FakeLlmToolClient.Failing(Error.Unexpected("ai.timeout", "down"));

        var result = await CreateSut(client).GradeAsync(Request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ai.timeout");
    }

    [Fact]
    public async Task GradeAsyncWithMissingScoreReturnsParseFailure()
    {
        var client = FakeLlmToolClient.Returning(new JsonObject { ["verdict"] = "Correct" });

        var result = await CreateSut(client).GradeAsync(Request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ai.parse_failed");
    }
}
