using FluentAssertions;

using Hangfire;
using Hangfire.Common;
using Hangfire.States;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Features.DeliverReminderNow;
using Memoria.Reminders.Persistence;
using Memoria.Reminders.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

namespace Memoria.Reminders.UnitTests.Features.DeliverReminderNow;

public sealed class DeliverReminderNowCommandHandlerTests
{
    private static readonly DateTime ClockUtc = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

    private readonly IBackgroundJobClient _hangfire = Substitute.For<IBackgroundJobClient>();

    private DeliverReminderNowCommandHandler CreateSut(RemindersDbContext db) => new(db, _hangfire);

    private static Reminder PendingReminder(Guid userId) =>
        new(Guid.NewGuid(), userId, stageNumber: 1, ClockUtc);

    [Fact]
    public async Task HandlePendingReminderEnqueuesSendJob()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var reminder = PendingReminder(userId);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new DeliverReminderNowCommand(reminder.Id, userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _hangfire.Received(1).Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    [Fact]
    public async Task HandleUnknownReminderReturnsNotFound()
    {
        await using var db = RemindersDbContextTestFactory.Create();

        var result = await CreateSut(db).Handle(
            new DeliverReminderNowCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        _hangfire.DidNotReceive().Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    [Fact]
    public async Task HandleForeignReminderReturnsForbidden()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var reminder = PendingReminder(Guid.NewGuid());
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new DeliverReminderNowCommand(reminder.Id, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);
        _hangfire.DidNotReceive().Create(Arg.Any<Job>(), Arg.Any<IState>());
    }

    [Fact]
    public async Task HandleNonPendingReminderReturnsConflict()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var reminder = PendingReminder(userId);
        reminder.BeginSending();
        reminder.MarkSent(messageId: 1, ClockUtc); // now Sent, not Pending
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new DeliverReminderNowCommand(reminder.Id, userId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("reminders.not_pending");
        _hangfire.DidNotReceive().Create(Arg.Any<Job>(), Arg.Any<IState>());
    }
}
