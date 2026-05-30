using FluentAssertions;

using MediatR;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Domain;
using Memoria.Cards.Features.UnpauseCard;
using Memoria.Cards.UnitTests.Infrastructure;
using Memoria.Reminders.Contracts.Commands;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Cards.UnitTests.Features.UnpauseCard;

public sealed class UnpauseCardCommandHandlerTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero));
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    public UnpauseCardCommandHandlerTests()
    {
        _mediator.Send(Arg.Any<ScheduleRemindersForCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Success(Unit.Value));
    }

    [Fact]
    public async Task HandleHappyPathClearsPauseAndSchedulesAtStoredStage()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var card = new Card(userId, "T", "B", _clock.GetUtcNow().UtcDateTime);
        card.Pause(stage: 3, _clock.GetUtcNow().UtcDateTime);
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var sut = new UnpauseCardCommandHandler(db, _mediator, _clock);
        var result = await sut.Handle(
            new UnpauseCardCommand(userId, card.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _mediator.Received(1).Send(
            Arg.Is<ScheduleRemindersForCardCommand>(c =>
                c.CardId == card.Id && c.UserId == userId && c.Stage == 3),
            Arg.Any<CancellationToken>());

        var persisted = await db.Cards.FirstAsync(c => c.Id == card.Id);
        persisted.IsPaused.Should().BeFalse();
        persisted.PausedAtStage.Should().BeNull();
    }

    [Fact]
    public async Task HandleWhenStageWasNullSchedulesFromStartByPassingNull()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var card = new Card(userId, "T", "B", _clock.GetUtcNow().UtcDateTime);
        card.Pause(stage: null, _clock.GetUtcNow().UtcDateTime);
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var sut = new UnpauseCardCommandHandler(db, _mediator, _clock);
        var result = await sut.Handle(
            new UnpauseCardCommand(userId, card.Id),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _mediator.Received(1).Send(
            Arg.Is<ScheduleRemindersForCardCommand>(c => c.Stage == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleForeignCardReturnsForbidden()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var owner = Guid.NewGuid();
        var card = new Card(owner, "T", "B", _clock.GetUtcNow().UtcDateTime);
        card.Pause(stage: 1, _clock.GetUtcNow().UtcDateTime);
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var sut = new UnpauseCardCommandHandler(db, _mediator, _clock);
        var result = await sut.Handle(
            new UnpauseCardCommand(Guid.NewGuid(), card.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task HandleNotPausedReturnsConflict()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var card = new Card(userId, "T", "B", _clock.GetUtcNow().UtcDateTime);
        db.Cards.Add(card);
        await db.SaveChangesAsync();

        var sut = new UnpauseCardCommandHandler(db, _mediator, _clock);
        var result = await sut.Handle(
            new UnpauseCardCommand(userId, card.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("cards.not_paused");
    }

    [Fact]
    public async Task HandleUnknownCardReturnsNotFound()
    {
        await using var db = CardsDbContextTestFactory.Create();
        var sut = new UnpauseCardCommandHandler(db, _mediator, _clock);

        var result = await sut.Handle(
            new UnpauseCardCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
