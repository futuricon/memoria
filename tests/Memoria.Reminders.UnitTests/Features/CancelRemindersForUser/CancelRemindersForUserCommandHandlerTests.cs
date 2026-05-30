using FluentAssertions;

using Hangfire;
using Hangfire.States;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Features.CancelRemindersForUser;
using Memoria.Reminders.UnitTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Memoria.Reminders.UnitTests.Features.CancelRemindersForUser;

public sealed class CancelRemindersForUserCommandHandlerTests
{
    private static readonly DateTime ScheduledAt = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(ScheduledAt, TimeSpan.Zero));
    private readonly IBackgroundJobClient _hangfire = Substitute.For<IBackgroundJobClient>();

    private CancelRemindersForUserCommandHandler CreateSut(Persistence.RemindersDbContext db) =>
        new(db, _hangfire, _clock, NullLogger<CancelRemindersForUserCommandHandler>.Instance);

    [Fact]
    public async Task HandleRemovesPendingAndDeletesHangfireJob()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var user = Guid.NewGuid();
        var pending = new Reminder(Guid.NewGuid(), user, 1, ScheduledAt);
        pending.AttachHangfireJob("job-pending");
        db.Reminders.Add(pending);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new CancelRemindersForUserCommand(user), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.Reminders.CountAsync()).Should().Be(0);
        // IBackgroundJobClient.Delete is an extension that internally calls
        // ChangeState(jobId, DeletedState, ...); NSubstitute only sees the
        // underlying interface call.
        _hangfire.Received(1).ChangeState("job-pending", Arg.Any<IState>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task HandleCancelsSendingAndSentRemindersKeepingHistory()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var user = Guid.NewGuid();
        var sending = new Reminder(Guid.NewGuid(), user, 1, ScheduledAt);
        sending.BeginSending();
        var sent = new Reminder(Guid.NewGuid(), user, 1, ScheduledAt);
        sent.BeginSending();
        sent.MarkSent(messageId: 42, ScheduledAt);
        db.Reminders.AddRange(sending, sent);
        await db.SaveChangesAsync();

        await CreateSut(db).Handle(
            new CancelRemindersForUserCommand(user), CancellationToken.None);

        var all = await db.Reminders.ToListAsync();
        all.Should().HaveCount(2);
        all.Should().OnlyContain(r => r.Status == ReminderStatus.Cancelled);
    }

    [Fact]
    public async Task HandleLeavesTerminalStatusesUntouched()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var user = Guid.NewGuid();
        var confirmed = new Reminder(Guid.NewGuid(), user, 1, ScheduledAt);
        confirmed.BeginSending();
        confirmed.MarkSent(1, ScheduledAt);
        confirmed.Confirm(ScheduledAt);
        db.Reminders.Add(confirmed);
        await db.SaveChangesAsync();

        await CreateSut(db).Handle(
            new CancelRemindersForUserCommand(user), CancellationToken.None);

        (await db.Reminders.SingleAsync()).Status.Should().Be(ReminderStatus.Confirmed);
    }

    [Fact]
    public async Task HandleSkipsRemindersOwnedByOtherUsers()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var user = Guid.NewGuid();
        var other = Guid.NewGuid();
        db.Reminders.AddRange(
            new Reminder(Guid.NewGuid(), user, 1, ScheduledAt),
            new Reminder(Guid.NewGuid(), other, 1, ScheduledAt));
        await db.SaveChangesAsync();

        await CreateSut(db).Handle(
            new CancelRemindersForUserCommand(user), CancellationToken.None);

        (await db.Reminders.CountAsync(r => r.UserId == other)).Should().Be(1);
    }
}
