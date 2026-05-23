using FluentAssertions;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Contracts.Events;
using Memoria.Cards.Domain;
using Memoria.Cards.Features.SoftDeleteCard;
using Memoria.Cards.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.UnitTests.Features.SoftDeleteCard;

public sealed class SoftDeleteCardCommandHandlerTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero));
    private readonly IPublisher _publisher = Substitute.For<IPublisher>();

    [Fact]
    public async Task HandleHappyPathSetsDeletedAtAndPublishesEvent()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var card = new Card(userId, "Title", "Body", _clock.GetUtcNow().UtcDateTime);
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var sut = new SoftDeleteCardCommandHandler(db, _clock, _publisher);
        var result = await sut.Handle(
            new SoftDeleteCardCommand(userId, card.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persisted = await db.Cards.IgnoreQueryFilters().FirstAsync(c => c.Id == card.Id);
        persisted.DeletedAt.Should().NotBeNull();

        await _publisher.Received(1)
            .Publish(Arg.Any<CardSoftDeletedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleForeignCardReturnsForbidden()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var card = new Card(owner, "T", "B", _clock.GetUtcNow().UtcDateTime);
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var sut = new SoftDeleteCardCommandHandler(db, _clock, _publisher);
        var result = await sut.Handle(
            new SoftDeleteCardCommand(attacker, card.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task HandleUnknownCardReturnsNotFound()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var sut = new SoftDeleteCardCommandHandler(db, _clock, _publisher);

        var result = await sut.Handle(
            new SoftDeleteCardCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
