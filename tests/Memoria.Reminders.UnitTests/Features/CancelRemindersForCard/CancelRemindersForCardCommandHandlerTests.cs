using FluentAssertions;

using Hangfire;
using Hangfire.Common;
using Hangfire.States;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Features.CancelRemindersForCard;
using Memoria.Reminders.Persistence;
using Memoria.Reminders.Services;
using Memoria.Reminders.UnitTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Memoria.Reminders.UnitTests.Features.CancelRemindersForCard;

public sealed class CancelRemindersForCardCommandHandlerTests
{
    private static readonly DateTime ClockUtc = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
    private const int SampleMessageId = 42;

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(ClockUtc, TimeSpan.Zero));
    private readonly IBackgroundJobClient _hangfire = Substitute.For<IBackgroundJobClient>();

    private CancelRemindersForCardCommandHandler CreateSut(RemindersDbContext db) =>
        new(db, _hangfire,
            new DueRemindersDispatcher(db, _hangfire, NullLogger<DueRemindersDispatcher>.Instance),
            _clock,
            NullLogger<CancelRemindersForCardCommandHandler>.Instance);

    private static Reminder NewPendingReminder(Guid cardId, Guid? userId = null, string? hangfireJobId = null)
    {
        var r = new Reminder(cardId, userId ?? Guid.NewGuid(), stageNumber: 1, ClockUtc);
        if (hangfireJobId is not null)
        {
            r.AttachHangfireJob(hangfireJobId);
        }
        return r;
    }

    [Fact]
    public async Task HandlePendingRemindersAreHardDeletedAndHangfireJobsDeleted()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var cardId = Guid.NewGuid();
        var reminder = NewPendingReminder(cardId, hangfireJobId: "job-1");
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(new CancelRemindersForCardCommand(cardId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _hangfire.Received(1).ChangeState("job-1", Arg.Any<IState>(), Arg.Any<string?>());
        (await db.Reminders.AnyAsync(r => r.Id == reminder.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task HandleSendingReminderIsMarkedCancelled()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var cardId = Guid.NewGuid();
        var reminder = NewPendingReminder(cardId);
        reminder.BeginSending();
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(new CancelRemindersForCardCommand(cardId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await db.Reminders.FirstAsync(r => r.Id == reminder.Id);
        persisted.Status.Should().Be(ReminderStatus.Cancelled);
        persisted.ConfirmedAt.Should().Be(ClockUtc);
        _hangfire.DidNotReceiveWithAnyArgs().ChangeState(default!, default!, default!);
    }

    [Fact]
    public async Task HandleSentReminderIsCancelledSoTheUserQueueDoesNotStall()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var cardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reminder = NewPendingReminder(cardId, userId);
        reminder.BeginSending();
        reminder.MarkSent(SampleMessageId, ClockUtc);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(new CancelRemindersForCardCommand(cardId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await db.Reminders.FirstAsync(r => r.Id == reminder.Id);
        persisted.Status.Should().Be(ReminderStatus.Cancelled,
            because: "leaving a Sent reminder for a deleted card would deadlock the single-in-flight queue");
        persisted.ConfirmedAt.Should().Be(ClockUtc);
    }

    [Fact]
    public async Task HandleCancellingSentEnqueuesNextOverdueForSameUser()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var deletedCardId = Guid.NewGuid();
        var otherCardId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var inFlight = NewPendingReminder(deletedCardId, userId);
        inFlight.BeginSending();
        inFlight.MarkSent(SampleMessageId, ClockUtc);

        var overduePending = new Reminder(otherCardId, userId, stageNumber: 1, ClockUtc.AddMinutes(-10));
        db.Reminders.AddRange(inFlight, overduePending);
        await db.SaveChangesAsync();

        await CreateSut(db).Handle(new CancelRemindersForCardCommand(deletedCardId), CancellationToken.None);

        _hangfire.Received(1).Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    [Fact]
    public async Task HandleHangfireDeleteThrowingDoesNotFailHandler()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var cardId = Guid.NewGuid();
        var reminder = NewPendingReminder(cardId, hangfireJobId: "job-1");
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        _hangfire
            .ChangeState(Arg.Any<string>(), Arg.Any<IState>(), Arg.Any<string?>())
            .Throws(new InvalidOperationException("Hangfire storage is down"));

        var result = await CreateSut(db).Handle(new CancelRemindersForCardCommand(cardId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.Reminders.AnyAsync(r => r.Id == reminder.Id)).Should().BeFalse(
            because: "Hangfire deletion errors must not block reminder removal");
    }
}
