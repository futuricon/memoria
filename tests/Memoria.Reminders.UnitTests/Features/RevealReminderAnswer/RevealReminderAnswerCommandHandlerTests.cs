using FluentAssertions;

using MediatR;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Queries;
using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Features.RevealReminderAnswer;
using Memoria.Reminders.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

using NSubstitute;

namespace Memoria.Reminders.UnitTests.Features.RevealReminderAnswer;

public sealed class RevealReminderAnswerCommandHandlerTests
{
    private static readonly DateTime ClockUtc = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
    private const int SampleMessageId = 42;

    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private static Reminder NewPendingReminder(Guid? userId = null) =>
        new(Guid.NewGuid(), userId ?? Guid.NewGuid(), stageNumber: 1, ClockUtc);

    private static Reminder NewSentReminder(Guid userId)
    {
        var r = NewPendingReminder(userId);
        r.BeginSending();
        r.MarkSent(SampleMessageId, ClockUtc);
        return r;
    }

    private static Reminder NewConfirmedReminder(Guid userId)
    {
        var r = NewSentReminder(userId);
        r.Confirm(ClockUtc);
        return r;
    }

    private static CardDto FakeCardDto(Guid cardId, string title = "Title", string body = "Body") =>
        new(cardId, title, body, new[] { "tag" }, ClockUtc, ClockUtc, CardType.Note);

    [Fact]
    public async Task HandleSentReminderReturnsCardBody()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var reminder = NewSentReminder(userId);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        _mediator
            .Send(Arg.Any<GetCardByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<CardDto>.Success(FakeCardDto(reminder.CardId, "PostgreSQL VACUUM", "vacuum deletes dead tuples")));

        var sut = new RevealReminderAnswerCommandHandler(db, _mediator, TimeProvider.System);

        var result = await sut.Handle(
            new RevealReminderAnswerCommand(reminder.Id, userId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CardId.Should().Be(reminder.CardId);
        result.Value.Title.Should().Be("PostgreSQL VACUUM");
        result.Value.Body.Should().Be("vacuum deletes dead tuples");
    }

    [Fact]
    public async Task HandleConfirmedReminderStillReturnsCardBody()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var reminder = NewConfirmedReminder(userId);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        _mediator
            .Send(Arg.Any<GetCardByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<CardDto>.Success(FakeCardDto(reminder.CardId)));

        var sut = new RevealReminderAnswerCommandHandler(db, _mediator, TimeProvider.System);

        var result = await sut.Handle(
            new RevealReminderAnswerCommand(reminder.Id, userId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task HandlePendingReminderTransitionsToSentAndReturnsBody()
    {
        // SPA-initiated practice: the user opened a Pending reminder before
        // the bot delivered it. Reveal lazily transitions Pending → Sent
        // so the subsequent grade flow works.
        await using var db = RemindersDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var reminder = NewPendingReminder(userId);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        _mediator
            .Send(Arg.Any<GetCardByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<CardDto>.Success(FakeCardDto(reminder.CardId)));

        var sut = new RevealReminderAnswerCommandHandler(db, _mediator, TimeProvider.System);

        var result = await sut.Handle(
            new RevealReminderAnswerCommand(reminder.Id, userId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // Status mutated to Sent, MessageId null (no Telegram delivery).
        var persisted = await db.Reminders.FindAsync(reminder.Id);
        persisted!.Status.Should().Be(ReminderStatus.Sent);
        persisted.MessageId.Should().BeNull();
        persisted.SentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleUnknownReminderReturnsNotFound()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var sut = new RevealReminderAnswerCommandHandler(db, _mediator, TimeProvider.System);

        var result = await sut.Handle(
            new RevealReminderAnswerCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleForeignReminderReturnsForbidden()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var reminder = NewSentReminder(owner);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var sut = new RevealReminderAnswerCommandHandler(db, _mediator, TimeProvider.System);

        var result = await sut.Handle(
            new RevealReminderAnswerCommand(reminder.Id, attacker),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);

        await _mediator.DidNotReceive()
            .Send(Arg.Any<GetCardByIdQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleSoftDeletedCardReturnsNotFound()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var reminder = NewSentReminder(userId);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        _mediator
            .Send(Arg.Any<GetCardByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<CardDto>.Failure(Error.NotFound("cards.not_found", "soft-deleted")));

        var sut = new RevealReminderAnswerCommandHandler(db, _mediator, TimeProvider.System);

        var result = await sut.Handle(
            new RevealReminderAnswerCommand(reminder.Id, userId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
