using FluentAssertions;

using Hangfire;
using Hangfire.Common;
using Hangfire.States;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Features.ConfirmReminder;
using Memoria.Reminders.Persistence;
using Memoria.Reminders.Services;
using Memoria.Reminders.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Memoria.Reminders.UnitTests.Features.ConfirmReminder;

public sealed class ConfirmReminderCommandHandlerTests
{
    private static readonly DateTime ClockUtc = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
    private const int SampleMessageId = 42;

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(ClockUtc, TimeSpan.Zero));
    private readonly IBackgroundJobClient _hangfire = Substitute.For<IBackgroundJobClient>();

    private ConfirmReminderCommandHandler CreateSut(RemindersDbContext db) =>
        new(db, _clock,
            new DueRemindersDispatcher(db, _hangfire, NullLogger<DueRemindersDispatcher>.Instance));

    private static Reminder NewSentReminder(Guid userId)
    {
        var r = new Reminder(Guid.NewGuid(), userId, stageNumber: 1, ClockUtc);
        r.BeginSending();
        r.MarkSent(SampleMessageId, ClockUtc);
        return r;
    }

    [Fact]
    public async Task HandleSentReminderTransitionsToConfirmed()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var reminder = NewSentReminder(userId);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new ConfirmReminderCommand(reminder.Id, userId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await db.Reminders.FirstAsync(r => r.Id == reminder.Id);
        persisted.Status.Should().Be(ReminderStatus.Confirmed);
        persisted.ConfirmedAt.Should().Be(ClockUtc);
    }

    [Fact]
    public async Task HandleUnknownReminderReturnsNotFound()
    {
        await using var db = RemindersDbContextTestFactory.Create();

        var result = await CreateSut(db).Handle(
            new ConfirmReminderCommand(Guid.NewGuid(), Guid.NewGuid()),
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

        var result = await CreateSut(db).Handle(
            new ConfirmReminderCommand(reminder.Id, attacker),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);

        var persisted = await db.Reminders.FirstAsync(r => r.Id == reminder.Id);
        persisted.Status.Should().Be(ReminderStatus.Sent,
            because: "ownership check must reject before touching the entity");
    }

    [Fact]
    public async Task HandleReminderInWrongStatusReturnsConflict()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var pending = new Reminder(Guid.NewGuid(), userId, stageNumber: 1, ClockUtc);
        db.Reminders.Add(pending);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new ConfirmReminderCommand(pending.Id, userId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("reminders.invalid_transition");
    }

    [Fact]
    public async Task HandleConfirmEnqueuesNextOverduePendingReminder()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var inFlight = NewSentReminder(userId);
        var overduePending = new Reminder(
            Guid.NewGuid(), userId, stageNumber: 1, ClockUtc.AddMinutes(-10));
        db.Reminders.AddRange(inFlight, overduePending);
        await db.SaveChangesAsync();

        await CreateSut(db).Handle(
            new ConfirmReminderCommand(inFlight.Id, userId), CancellationToken.None);

        _hangfire.Received(1).Create(Arg.Any<Job>(), Arg.Any<IState>());
    }
}
