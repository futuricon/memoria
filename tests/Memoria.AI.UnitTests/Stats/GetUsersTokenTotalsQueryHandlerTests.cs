using FluentAssertions;

using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Contracts.Queries;
using Memoria.AI.Domain;
using Memoria.AI.Features.Stats;
using Memoria.AI.Options;
using Memoria.AI.Pricing;
using Memoria.AI.UnitTests.Infrastructure;

using Microsoft.Extensions.Options;

namespace Memoria.AI.UnitTests.Stats;

public sealed class GetUsersTokenTotalsQueryHandlerTests
{
    private static readonly Guid Alice = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Bob = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static AiOptions PricingOptions() => new()
    {
        Pricing = new(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-sonnet-4-6"] = new() { InputUsdPerMillion = 3.00m, OutputUsdPerMillion = 15.00m },
            ["deepseek-chat"] = new() { InputUsdPerMillion = 0.27m, OutputUsdPerMillion = 1.10m },
        },
    };

    private static GetUsersTokenTotalsQueryHandler CreateSut(Persistence.AiDbContext db)
    {
        var monitor = new TestMonitor(PricingOptions());
        return new GetUsersTokenTotalsQueryHandler(db, new AiModelPricing(monitor));
    }

    [Fact]
    public async Task HandleEmptyUserIdsReturnsEmptyDictionary()
    {
        await using var db = AiDbContextTestFactory.Create();
        var sut = CreateSut(db);

        var result = await sut.Handle(
            new GetUsersTokenTotalsQuery(Array.Empty<Guid>()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAggregatesTokensCallCountAndLastCallAcrossModels()
    {
        await using var db = AiDbContextTestFactory.Create();
        var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        db.Usage.AddRange(
            new AiUsage(Alice, AiOperation.AnswerGrading, "claude-sonnet-4-6", 1_000_000, 500_000, false, now),
            new AiUsage(Alice, AiOperation.AnswerGrading, "claude-sonnet-4-6", 500_000, 100_000, false, now.AddMinutes(-5)),
            new AiUsage(Alice, AiOperation.QuestionCardValidation, "deepseek-chat", 1_000_000, 1_000_000, false, now.AddHours(-1)),
            new AiUsage(Bob, AiOperation.AnswerGrading, "claude-sonnet-4-6", 100, 200, true, now));
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(
            new GetUsersTokenTotalsQuery(new[] { Alice, Bob }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var totals = result.Value!;

        totals.Should().ContainKey(Alice);
        var alice = totals[Alice];
        alice.CallCount.Should().Be(3);
        alice.TotalInputTokens.Should().Be(2_500_000);
        alice.TotalOutputTokens.Should().Be(1_600_000);
        alice.LastCallAt.Should().Be(now);
        // 1.5M input @ $3 + 600k output @ $15 = $4.50 + $9.00 = $13.50
        // + 1M input @ $0.27 + 1M output @ $1.10 = $0.27 + $1.10 = $1.37
        // = $14.87
        alice.EstimatedCostUsd.Should().Be(14.87m);

        totals.Should().ContainKey(Bob);
        totals[Bob].CallCount.Should().Be(1);
        totals[Bob].TotalInputTokens.Should().Be(100);
    }

    [Fact]
    public async Task HandleSkipsUsersNotInRequest()
    {
        await using var db = AiDbContextTestFactory.Create();
        db.Usage.Add(
            new AiUsage(Bob, AiOperation.AnswerGrading, "claude-sonnet-4-6", 100, 100, false, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(
            new GetUsersTokenTotalsQuery(new[] { Alice }), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().BeEmpty();
    }

    private sealed class TestMonitor : IOptionsMonitor<AiOptions>
    {
        public TestMonitor(AiOptions value) => CurrentValue = value;
        public AiOptions CurrentValue { get; }
        public AiOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AiOptions, string?> listener) => null;
    }
}
