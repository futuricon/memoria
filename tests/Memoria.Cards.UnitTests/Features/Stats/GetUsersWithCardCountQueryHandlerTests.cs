using FluentAssertions;

using Memoria.Cards.Contracts.Queries;
using Memoria.Cards.Domain;
using Memoria.Cards.Features.Stats;
using Memoria.Cards.UnitTests.Infrastructure;

namespace Memoria.Cards.UnitTests.Features.Stats;

public sealed class GetUsersWithCardCountQueryHandlerTests
{
    [Fact]
    public async Task HandleCountsDistinctOwners()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();
        db.Cards.AddRange(
            new Card(u1, "A1", "body", now),
            new Card(u1, "A2", "body", now),
            new Card(u2, "B1", "body", now));
        await db.SaveChangesAsync();

        var sut = new GetUsersWithCardCountQueryHandler(db);
        var result = await sut.Handle(new GetUsersWithCardCountQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
    }

    [Fact]
    public async Task HandleExcludesSoftDeletedCards()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var now = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var card = new Card(Guid.NewGuid(), "Title", "body", now);
        card.SoftDelete(now.AddHours(1));
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var sut = new GetUsersWithCardCountQueryHandler(db);
        var result = await sut.Handle(new GetUsersWithCardCountQuery(), CancellationToken.None);

        result.Value.Should().Be(0);
    }
}
