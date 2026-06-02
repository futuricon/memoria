using FluentAssertions;

using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Contracts.Events;
using Memoria.AI.Features.RecordAiUsage;
using Memoria.AI.UnitTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Memoria.AI.UnitTests.RecordAiUsage;

public sealed class AiUsageRecordedHandlerTests
{
    [Fact]
    public async Task HandlePersistsAiUsageRow()
    {
        await using var db = AiDbContextTestFactory.Create();
        var sut = new AiUsageRecordedHandler(db, NullLogger<AiUsageRecordedHandler>.Instance);

        var userId = Guid.NewGuid();
        var occurredAt = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var notification = new AiUsageRecorded(
            userId,
            AiOperation.AnswerGrading,
            "claude-sonnet-4-6",
            InputTokens: 123,
            OutputTokens: 45,
            IsFailure: false,
            occurredAt);

        await sut.Handle(notification, CancellationToken.None);

        var row = await db.Usage.SingleAsync();
        row.UserId.Should().Be(userId);
        row.Operation.Should().Be(AiOperation.AnswerGrading);
        row.Model.Should().Be("claude-sonnet-4-6");
        row.InputTokens.Should().Be(123);
        row.OutputTokens.Should().Be(45);
        row.IsFailure.Should().BeFalse();
        row.OccurredAt.Should().Be(occurredAt);
    }

    [Fact]
    public async Task HandlePersistsFailureRowWithZeroTokens()
    {
        await using var db = AiDbContextTestFactory.Create();
        var sut = new AiUsageRecordedHandler(db, NullLogger<AiUsageRecordedHandler>.Instance);

        var notification = new AiUsageRecorded(
            Guid.NewGuid(),
            AiOperation.QuestionCardValidation,
            "deepseek-chat",
            InputTokens: 0,
            OutputTokens: 0,
            IsFailure: true,
            new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));

        await sut.Handle(notification, CancellationToken.None);

        var row = await db.Usage.SingleAsync();
        row.IsFailure.Should().BeTrue();
        row.InputTokens.Should().Be(0);
        row.OutputTokens.Should().Be(0);
    }
}
