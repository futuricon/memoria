using System.Text.Json.Nodes;

using FluentAssertions;

using MediatR;

using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Contracts.Events;
using Memoria.AI.Llm;
using Memoria.AI.Options;
using Memoria.AI.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Memoria.AI.UnitTests.Llm;

public sealed class LlmAnswerGraderTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly GradingRequest Request =
        new(UserId, "What does VACUUM do?", "Reclaims dead tuples.", "It frees dead rows.");

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    private LlmAnswerGrader CreateSut(FakeLlmToolClient client) =>
        new(client,
            Microsoft.Extensions.Options.Options.Create(new AiOptions()),
            _mediator,
            _clock,
            NullLogger<LlmAnswerGrader>.Instance);

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

    [Fact]
    public async Task GradeAsyncOnSuccessPublishesUsageWithTokenCounts()
    {
        var client = FakeLlmToolClient.Returning(new JsonObject
        {
            ["score"] = 80,
            ["verdict"] = "Correct",
            ["feedback"] = "ok",
        });

        await CreateSut(client).GradeAsync(Request, CancellationToken.None);

        await _mediator.Received(1).Publish(
            Arg.Is<AiUsageRecorded>(e =>
                e.UserId == UserId
                && e.Operation == AiOperation.AnswerGrading
                && e.Model == FakeLlmToolClient.FakeModel
                && e.InputTokens == FakeLlmToolClient.FakeInputTokens
                && e.OutputTokens == FakeLlmToolClient.FakeOutputTokens
                && !e.IsFailure),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GradeAsyncOnFailurePublishesUsageWithZeroTokensAndIsFailureTrue()
    {
        var client = FakeLlmToolClient.Failing(Error.Unexpected("ai.timeout", "down"));

        await CreateSut(client).GradeAsync(Request, CancellationToken.None);

        await _mediator.Received(1).Publish(
            Arg.Is<AiUsageRecorded>(e =>
                e.UserId == UserId
                && e.Operation == AiOperation.AnswerGrading
                && e.InputTokens == 0
                && e.OutputTokens == 0
                && e.IsFailure),
            Arg.Any<CancellationToken>());
    }
}
