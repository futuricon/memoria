using FluentAssertions;

using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Contracts.Queries;
using Memoria.AI.Domain;
using Memoria.AI.Features.Stats;
using Memoria.AI.UnitTests.Infrastructure;

using Microsoft.Extensions.Time.Testing;

namespace Memoria.AI.UnitTests.Stats;

public sealed class GetAiFailureRateQueryHandlerTests
{
    private readonly FakeTimeProvider _clock =
        new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task HandleNoRowsReturnsZeroes()
    {
        await using var db = AiDbContextTestFactory.Create();
        var sut = new GetAiFailureRateQueryHandler(db, _clock);

        var result = await sut.Handle(new GetAiFailureRateQuery(30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalCalls.Should().Be(0);
        result.Value.FailedCalls.Should().Be(0);
        result.Value.FailureRate.Should().Be(0);
    }

    [Fact]
    public async Task HandleCountsFailuresWithinWindow()
    {
        await using var db = AiDbContextTestFactory.Create();
        var now = _clock.GetUtcNow().UtcDateTime;
        db.Usage.AddRange(
            new AiUsage(Guid.NewGuid(), AiOperation.AnswerGrading, "claude", 1, 1, false, now),
            new AiUsage(Guid.NewGuid(), AiOperation.AnswerGrading, "claude", 1, 1, true, now.AddDays(-2)),
            new AiUsage(Guid.NewGuid(), AiOperation.AnswerGrading, "claude", 1, 1, false, now.AddDays(-2)),
            // Outside the 30-day window
            new AiUsage(Guid.NewGuid(), AiOperation.AnswerGrading, "claude", 1, 1, true, now.AddDays(-40)));
        await db.SaveChangesAsync();

        var sut = new GetAiFailureRateQueryHandler(db, _clock);
        var result = await sut.Handle(new GetAiFailureRateQuery(30), CancellationToken.None);

        result.Value!.TotalCalls.Should().Be(3);
        result.Value.FailedCalls.Should().Be(1);
        result.Value.FailureRate.Should().BeApproximately(1d / 3d, 0.0001);
    }
}
