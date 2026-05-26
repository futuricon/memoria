using FluentAssertions;

using MediatR;

using Memoria.Cards.Contracts.Events;
using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Features.CardEventSubscribers;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

using NSubstitute;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Reminders.UnitTests.Features.CardEventSubscribers;

public sealed class CardSoftDeletedEventHandlerTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly FakeLogger<CardSoftDeletedEventHandler> _logger = new();

    private CardSoftDeletedEventHandler CreateSut() => new(_mediator, _logger);

    private static CardSoftDeletedEvent NewEvent(Guid? cardId = null) =>
        new(cardId ?? Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public async Task HandleDispatchesCancelRemindersCommand()
    {
        var cardId = Guid.NewGuid();
        var evt = NewEvent(cardId);

        _mediator
            .Send(Arg.Any<CancelRemindersForCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Success(Unit.Value));

        var sut = CreateSut();

        await sut.Handle(evt, CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<CancelRemindersForCardCommand>(c => c.CardId == cardId),
            Arg.Any<CancellationToken>());

        _logger.Collector.GetSnapshot().Should().BeEmpty(
            because: "happy path must not produce any warnings");
    }

    [Fact]
    public async Task HandleLogsErrorWhenCommandFails()
    {
        var evt = NewEvent();

        _mediator
            .Send(Arg.Any<CancelRemindersForCardCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Failure(Error.Unexpected("reminders.cancel_failed", "DB down")));

        var sut = CreateSut();

        await sut.Handle(evt, CancellationToken.None);

        var records = _logger.Collector.GetSnapshot();
        records.Should().ContainSingle()
            .Which.Level.Should().Be(LogLevel.Warning);
        records[0].Message.Should().Contain(evt.CardId.ToString());
        records[0].Message.Should().Contain("reminders.cancel_failed");
    }
}
