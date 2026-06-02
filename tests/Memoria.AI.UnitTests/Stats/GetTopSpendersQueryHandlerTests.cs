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

public sealed class GetTopSpendersQueryHandlerTests
{
    private static AiOptions PricingOptions() => new()
    {
        Pricing = new(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-sonnet-4-6"] = new() { InputUsdPerMillion = 3.00m, OutputUsdPerMillion = 15.00m },
        },
    };

    private static GetTopSpendersQueryHandler CreateSut(Persistence.AiDbContext db)
    {
        var monitor = new TestMonitor(PricingOptions());
        return new GetTopSpendersQueryHandler(db, new AiModelPricing(monitor));
    }

    [Fact]
    public async Task HandleSortsByEstimatedCostDesc()
    {
        await using var db = AiDbContextTestFactory.Create();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var carol = Guid.NewGuid();
        var now = DateTime.UtcNow;

        // Bob spends most, then Alice, then Carol.
        db.Usage.AddRange(
            new AiUsage(bob, AiOperation.AnswerGrading, "claude-sonnet-4-6", 5_000_000, 1_000_000, false, now),
            new AiUsage(alice, AiOperation.AnswerGrading, "claude-sonnet-4-6", 1_000_000, 1_000_000, false, now),
            new AiUsage(carol, AiOperation.AnswerGrading, "claude-sonnet-4-6", 100, 100, false, now));
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var result = await sut.Handle(new GetTopSpendersQuery(Top: 10), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Select(s => s.UserId).Should().ContainInOrder(bob, alice, carol);
    }

    [Fact]
    public async Task HandleClampsTopToAtLeastOneAndReturnsFew()
    {
        await using var db = AiDbContextTestFactory.Create();
        db.Usage.Add(new AiUsage(Guid.NewGuid(), AiOperation.AnswerGrading, "claude-sonnet-4-6", 100, 100, false, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var result = await sut.Handle(new GetTopSpendersQuery(Top: 0), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
    }

    private sealed class TestMonitor : IOptionsMonitor<AiOptions>
    {
        public TestMonitor(AiOptions value) => CurrentValue = value;
        public AiOptions CurrentValue { get; }
        public AiOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AiOptions, string?> listener) => null;
    }
}
