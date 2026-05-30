using FluentAssertions;

using MediatR;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Domain;
using Memoria.Cards.Features.PauseCard;
using Memoria.Cards.UnitTests.Infrastructure;
using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Contracts.Queries;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Cards.UnitTests.Features.PauseCard;

public sealed class PauseCardCommandHandlerTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero));
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    public PauseCardCommandHandlerTests()
    {
        _mediator.Send(Arg.Any<GetCurrentCardStageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<int?>.Success(2));
        _mediator.Send(Arg.Any<CancelRemindersForCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Success(Unit.Value));
    }

    [Fact]
    public async Task HandleHappyPathSnapshotsStageCancelsAndMarksPaused()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var card = new Card(userId, "T", "B", _clock.GetUtcNow().UtcDateTime);
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var sut = new PauseCardCommandHandler(db, _mediator, _clock);
        var result = await sut.Handle(
            new PauseCardCommand(userId, card.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        await _mediator.Received(1).Send(
            Arg.Is<GetCurrentCardStageQuery>(q => q.CardId == card.Id),
            Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(
            Arg.Is<CancelRemindersForCardCommand>(c => c.CardId == card.Id),
            Arg.Any<CancellationToken>());

        var persisted = await db.Cards.FirstAsync(c => c.Id == card.Id);
        persisted.IsPaused.Should().BeTrue();
        persisted.PausedAtStage.Should().Be(2);
    }

    [Fact]
    public async Task HandleWhenCardNeverHadReminderStoresNullStage()
    {
        _mediator.Send(Arg.Any<GetCurrentCardStageQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<int?>.Success(null));

        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var card = new Card(userId, "T", "B", _clock.GetUtcNow().UtcDateTime);
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var sut = new PauseCardCommandHandler(db, _mediator, _clock);
        var result = await sut.Handle(
            new PauseCardCommand(userId, card.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persisted = await db.Cards.FirstAsync(c => c.Id == card.Id);
        persisted.PausedAtStage.Should().BeNull();
    }

    [Fact]
    public async Task HandleForeignCardReturnsForbidden()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var owner = Guid.NewGuid();
        var card = new Card(owner, "T", "B", _clock.GetUtcNow().UtcDateTime);
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var sut = new PauseCardCommandHandler(db, _mediator, _clock);
        var result = await sut.Handle(
            new PauseCardCommand(Guid.NewGuid(), card.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task HandleAlreadyPausedReturnsConflict()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var card = new Card(userId, "T", "B", _clock.GetUtcNow().UtcDateTime);
        card.Pause(stage: 1, _clock.GetUtcNow().UtcDateTime);
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var sut = new PauseCardCommandHandler(db, _mediator, _clock);
        var result = await sut.Handle(
            new PauseCardCommand(userId, card.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("cards.already_paused");
    }

    [Fact]
    public async Task HandleUnknownCardReturnsNotFound()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var sut = new PauseCardCommandHandler(db, _mediator, _clock);

        var result = await sut.Handle(
            new PauseCardCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
