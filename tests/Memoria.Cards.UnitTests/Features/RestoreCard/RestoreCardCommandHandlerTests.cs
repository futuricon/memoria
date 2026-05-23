using FluentAssertions;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Contracts.Events;
using Memoria.Cards.Domain;
using Memoria.Cards.Features.RestoreCard;
using Memoria.Cards.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.UnitTests.Features.RestoreCard;

public sealed class RestoreCardCommandHandlerTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero));
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();

    [Fact]
    public async Task HandleSoftDeletedCardWithinRetentionRestoresIt()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var card = new Card(userId, "T", "B", _clock.GetUtcNow().UtcDateTime);
        card.SoftDelete(_clock.GetUtcNow().UtcDateTime.AddDays(-10));
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var sut = new RestoreCardCommandHandler(db, _clock, _publisher);
        var result = await sut.Handle(
            new RestoreCardCommand(userId, card.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persisted = await db.Cards.FirstAsync(c => c.Id == card.Id);
        persisted.DeletedAt.Should().BeNull();

        await _publisher.Received(1)
            .Publish(Arg.Any<CardRestoredEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleActiveCardReturnsConflict()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var card = new Card(userId, "T", "B", _clock.GetUtcNow().UtcDateTime);
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var sut = new RestoreCardCommandHandler(db, _clock, _publisher);
        var result = await sut.Handle(
            new RestoreCardCommand(userId, card.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public async Task HandleCardDeletedMoreThan90DaysAgoReturnsNotFound()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var card = new Card(userId, "T", "B", _clock.GetUtcNow().UtcDateTime);
        card.SoftDelete(_clock.GetUtcNow().UtcDateTime.AddDays(-91));
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var sut = new RestoreCardCommandHandler(db, _clock, _publisher);
        var result = await sut.Handle(
            new RestoreCardCommand(userId, card.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("cards.retention_expired");
    }
}
