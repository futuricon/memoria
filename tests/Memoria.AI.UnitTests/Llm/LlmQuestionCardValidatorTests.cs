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

public sealed class LlmQuestionCardValidatorTests
{
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly QuestionCardValidationRequest Request =
        new(UserId, "Q?", "A");

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    private LlmQuestionCardValidator CreateSut(FakeLlmToolClient client) =>
        new(client,
            Microsoft.Extensions.Options.Options.Create(new AiOptions()),
            _mediator,
            _clock,
            NullLogger<LlmQuestionCardValidator>.Instance);

    [Fact]
    public async Task ValidateAsyncCoherentReturnsNullReason()
    {
        var client = FakeLlmToolClient.Returning(new JsonObject
        {
            ["is_coherent"] = true,
            ["reason"] = string.Empty,
        });

        var result = await CreateSut(client).ValidateAsync(Request, CancellationToken.None);

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

        var result = await CreateSut(client).ValidateAsync(Request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsCoherent.Should().BeFalse();
        result.Value.Reason.Should().Be("Body is off-topic.");
    }

    [Fact]
    public async Task ValidateAsyncWithMissingFlagReturnsParseFailure()
    {
        var client = FakeLlmToolClient.Returning(new JsonObject { ["reason"] = "no flag" });

        var result = await CreateSut(client).ValidateAsync(Request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ai.parse_failed");
    }

    [Fact]
    public async Task ValidateAsyncPropagatesClientFailure()
    {
        var client = FakeLlmToolClient.Failing(Error.Unexpected("ai.bad_status", "500"));

        var result = await CreateSut(client).ValidateAsync(Request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ai.bad_status");
    }

    [Fact]
    public async Task ValidateAsyncOnSuccessPublishesUsageWithTokenCounts()
    {
        var client = FakeLlmToolClient.Returning(new JsonObject
        {
            ["is_coherent"] = true,
            ["reason"] = string.Empty,
        });

        await CreateSut(client).ValidateAsync(Request, CancellationToken.None);

        await _mediator.Received(1).Publish(
            Arg.Is<AiUsageRecorded>(e =>
                e.UserId == UserId
                && e.Operation == AiOperation.QuestionCardValidation
                && e.Model == FakeLlmToolClient.FakeModel
                && e.InputTokens == FakeLlmToolClient.FakeInputTokens
                && e.OutputTokens == FakeLlmToolClient.FakeOutputTokens
                && !e.IsFailure),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAsyncOnFailurePublishesUsageWithZeroTokensAndIsFailureTrue()
    {
        var client = FakeLlmToolClient.Failing(Error.Unexpected("ai.bad_status", "500"));

        await CreateSut(client).ValidateAsync(Request, CancellationToken.None);

        await _mediator.Received(1).Publish(
            Arg.Is<AiUsageRecorded>(e =>
                e.UserId == UserId
                && e.Operation == AiOperation.QuestionCardValidation
                && e.InputTokens == 0
                && e.OutputTokens == 0
                && e.IsFailure),
            Arg.Any<CancellationToken>());
    }
}
