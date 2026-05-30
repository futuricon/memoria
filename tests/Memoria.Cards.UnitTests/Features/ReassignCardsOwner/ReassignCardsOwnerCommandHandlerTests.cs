using FluentAssertions;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Domain;
using Memoria.Cards.Features.ReassignCardsOwner;
using Memoria.Cards.UnitTests.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Cards.UnitTests.Features.ReassignCardsOwner;

public sealed class ReassignCardsOwnerCommandHandlerTests
{
    private static readonly DateTime Now = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleReassignsActiveAndSoftDeletedCards()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        var active = new Card(source, "active", "body", Now);
        var trashed = new Card(source, "trashed", "body", Now);
        trashed.SoftDelete(Now);
        var unrelated = new Card(Guid.NewGuid(), "stay put", "body", Now);
        db.Cards.AddRange(active, trashed, unrelated);
        await db.SaveChangesAsync();

        var result = await new ReassignCardsOwnerCommandHandler(db).Handle(
            new ReassignCardsOwnerCommand(source, target), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        var allCards = await db.Cards.IgnoreQueryFilters().ToListAsync();
        allCards.Single(c => c.Id == active.Id).UserId.Should().Be(target);
        allCards.Single(c => c.Id == trashed.Id).UserId.Should().Be(target);
        allCards.Single(c => c.Id == trashed.Id).DeletedAt.Should().NotBeNull("soft-delete is preserved");
        allCards.Single(c => c.Id == unrelated.Id).UserId.Should().NotBe(target);
    }

    [Fact]
    public async Task HandleWhenTagNamesCollideRepointsCardTagsAndDeletesSourceTag()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();

        var sourceTag = new Tag(source, "dotnet", Now);
        var targetTag = new Tag(target, "dotnet", Now);
        db.Tags.AddRange(sourceTag, targetTag);

        var sourceCard = new Card(source, "src", "body", Now);
        db.Cards.Add(sourceCard);
        db.CardTags.Add(new CardTag(sourceCard.Id, sourceTag.Id));
        await db.SaveChangesAsync();

        await new ReassignCardsOwnerCommandHandler(db).Handle(
            new ReassignCardsOwnerCommand(source, target), CancellationToken.None);

        var tagsAfter = await db.Tags.ToListAsync();
        tagsAfter.Should().HaveCount(1, "source tag was deleted as duplicate");
        tagsAfter.Single().Id.Should().Be(targetTag.Id);

        var join = await db.CardTags.SingleAsync(ct => ct.CardId == sourceCard.Id);
        join.TagId.Should().Be(targetTag.Id, "join row was repointed to target's tag");

        (await db.Cards.SingleAsync(c => c.Id == sourceCard.Id)).UserId.Should().Be(target);
    }

    [Fact]
    public async Task HandleWhenNoTagCollisionRepointsTagOwnership()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();

        var sourceOnly = new Tag(source, "ef-core", Now);
        db.Tags.Add(sourceOnly);
        await db.SaveChangesAsync();

        await new ReassignCardsOwnerCommandHandler(db).Handle(
            new ReassignCardsOwnerCommand(source, target), CancellationToken.None);

        (await db.Tags.SingleAsync()).UserId.Should().Be(target);
    }

    [Fact]
    public async Task HandleIsIdempotentOnRerun()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        db.Cards.Add(new Card(source, "t", "b", Now));
        await db.SaveChangesAsync();

        var sut = new ReassignCardsOwnerCommandHandler(db);
        await sut.Handle(new ReassignCardsOwnerCommand(source, target), CancellationToken.None);
        var second = await sut.Handle(
            new ReassignCardsOwnerCommand(source, target), CancellationToken.None);

        second.Value.Should().Be(0);
    }

    [Fact]
    public async Task HandleWhenSourceEqualsTargetReturnsZero()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var user = Guid.NewGuid();

        var result = await new ReassignCardsOwnerCommandHandler(db).Handle(
            new ReassignCardsOwnerCommand(user, user), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }
}
