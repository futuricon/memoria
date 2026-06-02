using System.Text.Json;
using System.Text.Json.Nodes;

using FluentAssertions;

using MediatR;

using Memoria.AI.Contracts.Abstractions;
using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Contracts.Events;
using Memoria.AI.Llm;
using Memoria.AI.Options;
using Memoria.AI.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Memoria.AI.UnitTests.Quota;

/// <summary>
/// Phase 5 boundary: when <see cref="IAiQuotaService"/> returns a Failure,
/// the grader / validator must short-circuit BEFORE touching the LLM client
/// and BEFORE publishing a usage notification (a blocked call doesn't count
/// against spend or appear in the trend).
/// </summary>
public sealed class LlmQuotaGateTests
{
    private static readonly Error QuotaError =
        Error.Forbidden("ai.quota_exceeded", "Monthly token budget exhausted.");

    [Fact]
    public async Task GradeAsyncWithQuotaBlockedReturnsErrorAndSkipsLlmCall()
    {
        var llmCalled = false;
        var llm = new RecordingLlmToolClient(() => llmCalled = true);
        var mediator = Substitute.For<IMediator>();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        var quota = StubAiQuotaService.AlwaysBlock(QuotaError);

        var sut = new LlmAnswerGrader(
            llm,
            Microsoft.Extensions.Options.Options.Create(new AiOptions()),
            mediator,
            clock,
            quota,
            NullLogger<LlmAnswerGrader>.Instance);

        var result = await sut.GradeAsync(
            new GradingRequest(Guid.NewGuid(), "Q", "Ref", "User"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ai.quota_exceeded");
        result.Error.Type.Should().Be(ErrorType.Forbidden);

        llmCalled.Should().BeFalse("a quota-blocked call must never reach the wire");
        await mediator.DidNotReceive().Publish(
            Arg.Any<AiUsageRecorded>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAsyncWithQuotaBlockedReturnsErrorAndSkipsLlmCall()
    {
        var llmCalled = false;
        var llm = new RecordingLlmToolClient(() => llmCalled = true);
        var mediator = Substitute.For<IMediator>();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
        var quota = StubAiQuotaService.AlwaysBlock(QuotaError);

        var sut = new LlmQuestionCardValidator(
            llm,
            Microsoft.Extensions.Options.Options.Create(new AiOptions()),
            mediator,
            clock,
            quota,
            NullLogger<LlmQuestionCardValidator>.Instance);

        var result = await sut.ValidateAsync(
            new QuestionCardValidationRequest(Guid.NewGuid(), "Q?", "A"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ai.quota_exceeded");

        llmCalled.Should().BeFalse();
        await mediator.DidNotReceive().Publish(
            Arg.Any<AiUsageRecorded>(),
            Arg.Any<CancellationToken>());
    }

    private sealed class RecordingLlmToolClient : ILlmToolClient
    {
        private readonly Action _onCall;

        public RecordingLlmToolClient(Action onCall) => _onCall = onCall;

        public Task<Result<LlmToolInvocation>> InvokeToolAsync(
            string model,
            string system,
            string userMessage,
            string toolName,
            string toolDescription,
            JsonNode inputSchema,
            CancellationToken ct)
        {
            _onCall();
            // Returning success here would make the test pass for the wrong
            // reason if the gate ever broke — but the assertion on the flag
            // catches it.
            using var doc = JsonDocument.Parse("{}");
            return Task.FromResult(Result<LlmToolInvocation>.Success(
                new LlmToolInvocation(doc.RootElement.Clone(), 0, 0, model)));
        }
    }
}
