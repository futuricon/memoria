using FluentAssertions;

using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Contracts.Queries;
using Memoria.AI.Domain;
using Memoria.AI.Features.Stats;
using Memoria.AI.Options;
using Memoria.AI.Pricing;
using Memoria.AI.UnitTests.Infrastructure;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Memoria.AI.UnitTests.Stats;

public sealed class GetAiSpendTrendQueryHandlerTests
{
    private readonly FakeTimeProvider _clock =
        new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task HandleGroupsByDayProviderAndOperation()
    {
        await using var db = AiDbContextTestFactory.Create();
        var now = _clock.GetUtcNow().UtcDateTime;
        var d0 = now.Date;

        db.Usage.AddRange(
            new AiUsage(Guid.NewGuid(), AiOperation.AnswerGrading, "claude-sonnet-4-6", 100, 100, false, d0),
            new AiUsage(Guid.NewGuid(), AiOperation.AnswerGrading, "claude-sonnet-4-6", 200, 200, false, d0.AddHours(3)),
            new AiUsage(Guid.NewGuid(), AiOperation.QuestionCardValidation, "deepseek-chat", 1, 1, true, d0));
        await db.SaveChangesAsync();

        var monitor = new TestMonitor(new AiOptions());
        var sut = new GetAiSpendTrendQueryHandler(db, new AiModelPricing(monitor), _clock);

        var result = await sut.Handle(new GetAiSpendTrendQuery(30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var points = result.Value!;
        points.Should().HaveCount(2);
        var grading = points.Single(p => p.Operation == AiOperation.AnswerGrading);
        grading.Provider.Should().Be("claude");
        grading.CallCount.Should().Be(2);
        grading.InputTokens.Should().Be(300);

        var validation = points.Single(p => p.Operation == AiOperation.QuestionCardValidation);
        validation.Provider.Should().Be("deepseek");
        validation.CallCount.Should().Be(1);
    }

    private sealed class TestMonitor : IOptionsMonitor<AiOptions>
    {
        public TestMonitor(AiOptions value) => CurrentValue = value;
        public AiOptions CurrentValue { get; }
        public AiOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AiOptions, string?> listener) => null;
    }
}
