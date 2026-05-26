using FluentAssertions;

using Hangfire;
using Hangfire.States;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Features.CancelRemindersForCard;
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

    private CancelRemindersForCardCommandHandler CreateSut(Persistence.RemindersDbContext db) =>
        new(db, _hangfire, _clock, NullLogger<CancelRemindersForCardCommandHandler>.Instance);

    private static Reminder NewPendingReminder(Guid cardId, string? hangfireJobId = null)
    {
        var r = new Reminder(cardId, Guid.NewGuid(), stageNumber: 1, ClockUtc);
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

        var sut = CreateSut(db);

        var result = await sut.Handle(new CancelRemindersForCardCommand(cardId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // Hangfire.Delete(jobId) translates to IBackgroundJobClient.ChangeState(jobId, new DeletedState(), null).
        _hangfire.Received(1).ChangeState("job-1", Arg.Any<IState>(), Arg.Any<string?>());

        (await db.Reminders.AnyAsync(r => r.Id == reminder.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task HandleSendingReminderIsMarkedCancelled()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var cardId = Guid.NewGuid();
        var reminder = NewPendingReminder(cardId);
        reminder.BeginSending(); // now in Sending status
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new CancelRemindersForCardCommand(cardId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await db.Reminders.FirstAsync(r => r.Id == reminder.Id);
        persisted.Status.Should().Be(ReminderStatus.Cancelled);
        persisted.ConfirmedAt.Should().Be(ClockUtc);

        _hangfire.DidNotReceiveWithAnyArgs().ChangeState(default!, default!, default!);
    }

    [Fact]
    public async Task HandleSentRemindersAreNotTouched()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var cardId = Guid.NewGuid();
        var reminder = NewPendingReminder(cardId);
        reminder.BeginSending();
        reminder.MarkSent(SampleMessageId, ClockUtc);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        var result = await sut.Handle(new CancelRemindersForCardCommand(cardId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await db.Reminders.FirstAsync(r => r.Id == reminder.Id);
        persisted.Status.Should().Be(ReminderStatus.Sent);
        persisted.MessageId.Should().Be(SampleMessageId);

        _hangfire.DidNotReceiveWithAnyArgs().ChangeState(default!, default!, default!);
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

        var sut = CreateSut(db);

        var result = await sut.Handle(new CancelRemindersForCardCommand(cardId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.Reminders.AnyAsync(r => r.Id == reminder.Id)).Should().BeFalse(
            because: "Hangfire deletion errors must not block reminder removal");
    }
}
