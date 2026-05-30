using FluentAssertions;

using MediatR;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Queries;
using Memoria.Reminders.Contracts.Abstractions;
using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Jobs;
using Memoria.Reminders.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Memoria.Reminders.UnitTests.Jobs;

public sealed class SendReminderJobTests
{
    private static readonly DateTime ClockUtc = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
    private const int SampleMessageId = 42;

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(ClockUtc, TimeSpan.Zero));
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IReminderNotificationSender _sender = Substitute.For<IReminderNotificationSender>();
    private readonly ILogger<SendReminderJob> _logger = NullLogger<SendReminderJob>.Instance;

    private SendReminderJob CreateSut(Persistence.RemindersDbContext db) =>
        new(db, _mediator, _sender, _clock, _logger);

    private static Reminder NewPendingReminder(Guid? cardId = null, Guid? userId = null) =>
        new(cardId ?? Guid.NewGuid(), userId ?? Guid.NewGuid(), stageNumber: 1, ClockUtc);

    private static CardDto FakeCardDto(Guid cardId) =>
        new(cardId, "Title", "Body", new[] { "tag1", "tag2" }, ClockUtc, ClockUtc, CardType.Note);

    [Fact]
    public async Task ExecuteAsyncWithUnknownReminderIdReturnsWithoutAction()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var sut = CreateSut(db);

        await sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        await _sender.DidNotReceive()
            .SendReminderAsync(Arg.Any<ReminderNotification>(), Arg.Any<CancellationToken>());

        await _mediator.DidNotReceive()
            .Send(Arg.Any<GetCardByIdQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsyncDefersWhenAnotherReminderForSameUserIsSent()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var userId = Guid.NewGuid();

        var inFlight = new Reminder(Guid.NewGuid(), userId, stageNumber: 1, ClockUtc);
        inFlight.BeginSending();
        inFlight.MarkSent(SampleMessageId, ClockUtc);

        var pending = new Reminder(Guid.NewGuid(), userId, stageNumber: 1, ClockUtc);
        db.Reminders.AddRange(inFlight, pending);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        await sut.ExecuteAsync(pending.Id, CancellationToken.None);

        await _sender.DidNotReceive()
            .SendReminderAsync(Arg.Any<ReminderNotification>(), Arg.Any<CancellationToken>());

        var persisted = await db.Reminders.FirstAsync(r => r.Id == pending.Id);
        persisted.Status.Should().Be(ReminderStatus.Pending,
            because: "single-in-flight serialization should keep this reminder pending");
    }

    [Fact]
    public async Task ExecuteAsyncOnNonPendingReminderReturnsWithoutSending()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var reminder = NewPendingReminder();
        reminder.BeginSending();
        reminder.MarkSent(SampleMessageId, ClockUtc);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        await sut.ExecuteAsync(reminder.Id, CancellationToken.None);

        await _sender.DidNotReceive()
            .SendReminderAsync(Arg.Any<ReminderNotification>(), Arg.Any<CancellationToken>());

        await _mediator.DidNotReceive()
            .Send(Arg.Any<GetCardByIdQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsyncWhenCardIsSoftDeletedCancelsReminder()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var reminder = NewPendingReminder();
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        _mediator
            .Send(Arg.Any<GetCardByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<CardDto>.Failure(Error.NotFound("cards.not_found", "soft-deleted")));

        var sut = CreateSut(db);

        await sut.ExecuteAsync(reminder.Id, CancellationToken.None);

        var persisted = await db.Reminders.FirstAsync(r => r.Id == reminder.Id);
        persisted.Status.Should().Be(ReminderStatus.Cancelled);
        persisted.ConfirmedAt.Should().Be(ClockUtc);

        await _sender.DidNotReceive()
            .SendReminderAsync(Arg.Any<ReminderNotification>(), Arg.Any<CancellationToken>());

        await _mediator.DidNotReceive()
            .Send(Arg.Any<MarkReminderFailedCommand>(), Arg.Any<CancellationToken>());

        await _mediator.DidNotReceive()
            .Send(Arg.Any<MarkReminderSentCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsyncOnSenderSuccessDispatchesMarkReminderSentCommand()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var reminder = NewPendingReminder();
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        _mediator
            .Send(Arg.Any<GetCardByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<CardDto>.Success(FakeCardDto(reminder.CardId)));

        _sender
            .SendReminderAsync(Arg.Any<ReminderNotification>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(SampleMessageId));

        var sut = CreateSut(db);

        await sut.ExecuteAsync(reminder.Id, CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<MarkReminderSentCommand>(c =>
                c.ReminderId == reminder.Id && c.MessageId == SampleMessageId),
            Arg.Any<CancellationToken>());

        await _mediator.DidNotReceive()
            .Send(Arg.Any<MarkReminderFailedCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsyncOnSenderFailureDispatchesMarkReminderFailedCommand()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var reminder = NewPendingReminder();
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        _mediator
            .Send(Arg.Any<GetCardByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<CardDto>.Success(FakeCardDto(reminder.CardId)));

        _sender
            .SendReminderAsync(Arg.Any<ReminderNotification>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Failure(Error.NotFound("bot.no_identity", "No Telegram link")));

        var sut = CreateSut(db);

        await sut.ExecuteAsync(reminder.Id, CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<MarkReminderFailedCommand>(c =>
                c.ReminderId == reminder.Id && c.Reason == "bot.no_identity"),
            Arg.Any<CancellationToken>());

        await _mediator.DidNotReceive()
            .Send(Arg.Any<MarkReminderSentCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsyncTransitionsReminderToSendingBeforeCallingSender()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var reminder = NewPendingReminder();
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        _mediator
            .Send(Arg.Any<GetCardByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<CardDto>.Success(FakeCardDto(reminder.CardId)));

        _sender
            .SendReminderAsync(Arg.Any<ReminderNotification>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(SampleMessageId));

        var sut = CreateSut(db);

        await sut.ExecuteAsync(reminder.Id, CancellationToken.None);

        // MarkReminderSentCommand is mocked away (no real handler runs), so the
        // entity stays in Sending after the job's BeginSending+save step.
        var persisted = await db.Reminders.FirstAsync(r => r.Id == reminder.Id);
        persisted.Status.Should().Be(ReminderStatus.Sending);

        await _sender.Received(1)
            .SendReminderAsync(Arg.Any<ReminderNotification>(), Arg.Any<CancellationToken>());
    }
}
