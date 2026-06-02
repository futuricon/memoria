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

public sealed class GetAiSpendTotalsQueryHandlerTests
{
    private readonly FakeTimeProvider _clock =
        new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    private static AiOptions PricingOptions() => new()
    {
        Pricing = new(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-sonnet-4-6"] = new() { InputUsdPerMillion = 3.00m, OutputUsdPerMillion = 15.00m },
            ["deepseek-chat"] = new() { InputUsdPerMillion = 0.27m, OutputUsdPerMillion = 1.10m },
        },
    };

    [Fact]
    public async Task HandleAggregatesCostAcrossModelsWithinWindow()
    {
        await using var db = AiDbContextTestFactory.Create();
        var now = _clock.GetUtcNow().UtcDateTime;
        db.Usage.AddRange(
            new AiUsage(Guid.NewGuid(), AiOperation.AnswerGrading, "claude-sonnet-4-6", 1_000_000, 0, false, now),
            new AiUsage(Guid.NewGuid(), AiOperation.QuestionCardValidation, "deepseek-chat", 1_000_000, 0, false, now.AddDays(-5)),
            // Outside 30-day window
            new AiUsage(Guid.NewGuid(), AiOperation.AnswerGrading, "claude-sonnet-4-6", 999_999_999, 0, false, now.AddDays(-40)));
        await db.SaveChangesAsync();

        var monitor = new TestMonitor(PricingOptions());
        var sut = new GetAiSpendTotalsQueryHandler(db, new AiModelPricing(monitor), _clock);

        var result = await sut.Handle(new GetAiSpendTotalsQuery(30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CallCount.Should().Be(2);
        result.Value.TotalInputTokens.Should().Be(2_000_000);
        // $3.00 (Claude) + $0.27 (DeepSeek)
        result.Value.EstimatedCostUsd.Should().Be(3.27m);
    }

    private sealed class TestMonitor : IOptionsMonitor<AiOptions>
    {
        public TestMonitor(AiOptions value) => CurrentValue = value;
        public AiOptions CurrentValue { get; }
        public AiOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AiOptions, string?> listener) => null;
    }
}
