using FluentAssertions;

using MediatR;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Queries;
using Memoria.Reminders.Contracts.Commands;
using Memoria.Reviews.Contracts.Commands;
using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Features.RecordReview;
using Memoria.Reviews.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Reviews.UnitTests.Features.RecordReview;

public sealed class RecordReviewCommandHandlerTests
{
    private static readonly DateTime ClockUtc = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(ClockUtc, TimeSpan.Zero));
    private readonly FakeLogger<RecordReviewCommandHandler> _logger = new();

    private static CardDto MakeCardDto(Guid cardId, string title) =>
        new(cardId, title, "body", Array.Empty<string>(), ClockUtc, ClockUtc, CardType.Note);

    private void StubCardQuery(Guid userId, Guid cardId, Result<CardDto> result)
    {
        _mediator
            .Send(
                Arg.Is<GetCardByIdQuery>(q =>
                    q.UserId == userId && q.CardId == cardId && q.IncludeDeleted),
                Arg.Any<CancellationToken>())
            .Returns(result);
    }

    [Fact]
    public async Task HandleWithExistingCardCreatesReviewWithSnapshotOfCurrentTitle()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        StubCardQuery(userId, cardId, Result<CardDto>.Success(MakeCardDto(cardId, "PostgreSQL VACUUM")));

        var sut = new RecordReviewCommandHandler(db, _mediator, _clock, _logger);

        var result = await sut.Handle(
            new RecordReviewCommand(userId, cardId, ReminderId: null, Rating.Good, Note: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CardTitleSnapshot.Should().Be("PostgreSQL VACUUM");

        var persisted = await db.Reviews.FirstAsync();
        persisted.CardTitleSnapshot.Should().Be("PostgreSQL VACUUM");
        persisted.ReviewedAt.Should().Be(ClockUtc);
    }

    [Fact]
    public async Task HandleAfterCardTitleChangedDoesNotChangeExistingSnapshot()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        StubCardQuery(userId, cardId, Result<CardDto>.Success(MakeCardDto(cardId, "Original Title")));

        var sut = new RecordReviewCommandHandler(db, _mediator, _clock, _logger);

        var first = await sut.Handle(
            new RecordReviewCommand(userId, cardId, null, Rating.Good, null),
            CancellationToken.None);
        first.IsSuccess.Should().BeTrue();

        // Simulate edit: same card now returns new title.
        StubCardQuery(userId, cardId, Result<CardDto>.Success(MakeCardDto(cardId, "Edited Title")));

        var second = await sut.Handle(
            new RecordReviewCommand(userId, cardId, null, Rating.Hard, null),
            CancellationToken.None);
        second.IsSuccess.Should().BeTrue();

        var firstPersisted = await db.Reviews.FirstAsync(r => r.Id == first.Value!.Id);
        var secondPersisted = await db.Reviews.FirstAsync(r => r.Id == second.Value!.Id);

        firstPersisted.CardTitleSnapshot.Should().Be("Original Title",
            because: "snapshot must be immutable per addendum §13");
        secondPersisted.CardTitleSnapshot.Should().Be("Edited Title");
    }

    [Fact]
    public async Task HandleWithReminderIdDispatchesConfirmReminderCommand()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var reminderId = Guid.NewGuid();
        StubCardQuery(userId, cardId, Result<CardDto>.Success(MakeCardDto(cardId, "Title")));
        _mediator
            .Send(Arg.Any<ConfirmReminderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Success(Unit.Value));

        var sut = new RecordReviewCommandHandler(db, _mediator, _clock, _logger);

        var result = await sut.Handle(
            new RecordReviewCommand(userId, cardId, reminderId, Rating.Easy, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _mediator.Received(1).Send(
            Arg.Is<ConfirmReminderCommand>(c => c.ReminderId == reminderId && c.UserId == userId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWithoutReminderIdDoesNotDispatchConfirmReminderCommand()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        StubCardQuery(userId, cardId, Result<CardDto>.Success(MakeCardDto(cardId, "Title")));

        var sut = new RecordReviewCommandHandler(db, _mediator, _clock, _logger);

        var result = await sut.Handle(
            new RecordReviewCommand(userId, cardId, ReminderId: null, Rating.Forgot, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _mediator.DidNotReceive().Send(Arg.Any<ConfirmReminderCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleWhenConfirmCommandFailsStillSavesReviewAndReturnsSuccess()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var reminderId = Guid.NewGuid();
        StubCardQuery(userId, cardId, Result<CardDto>.Success(MakeCardDto(cardId, "Title")));
        _mediator
            .Send(Arg.Any<ConfirmReminderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<Unit>.Failure(Error.Conflict("reminders.invalid_transition", "wrong status")));

        var sut = new RecordReviewCommandHandler(db, _mediator, _clock, _logger);

        var result = await sut.Handle(
            new RecordReviewCommand(userId, cardId, reminderId, Rating.Good, null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.Reviews.CountAsync()).Should().Be(1);
        _logger.Collector.GetSnapshot()
            .Should().Contain(r => r.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task HandleWhenCardNotFoundReturnsError()
    {
        await using var db = ReviewsDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        StubCardQuery(userId, cardId, Result<CardDto>.Failure(Error.NotFound("cards.not_found", "Card not found")));

        var sut = new RecordReviewCommandHandler(db, _mediator, _clock, _logger);

        var result = await sut.Handle(
            new RecordReviewCommand(userId, cardId, null, Rating.Good, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        (await db.Reviews.CountAsync()).Should().Be(0);
    }
}
