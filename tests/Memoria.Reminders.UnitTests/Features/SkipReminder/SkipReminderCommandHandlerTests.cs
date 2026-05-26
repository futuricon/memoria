using FluentAssertions;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Features.SkipReminder;
using Memoria.Reminders.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Memoria.Reminders.UnitTests.Features.SkipReminder;

public sealed class SkipReminderCommandHandlerTests
{
    private static readonly DateTime ClockUtc = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
    private const int SampleMessageId = 42;

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(ClockUtc, TimeSpan.Zero));

    private static Reminder NewSentReminder(Guid userId)
    {
        var r = new Reminder(Guid.NewGuid(), userId, stageNumber: 1, ClockUtc);
        r.BeginSending();
        r.MarkSent(SampleMessageId, ClockUtc);
        return r;
    }

    [Fact]
    public async Task HandleSentReminderTransitionsToSkipped()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var reminder = NewSentReminder(userId);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var sut = new SkipReminderCommandHandler(db, _clock);

        var result = await sut.Handle(
            new SkipReminderCommand(reminder.Id, userId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await db.Reminders.FirstAsync(r => r.Id == reminder.Id);
        persisted.Status.Should().Be(ReminderStatus.Skipped);
        persisted.ConfirmedAt.Should().Be(ClockUtc);
    }

    [Fact]
    public async Task HandleAlreadySkippedReturnsConflict()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var reminder = NewSentReminder(userId);
        reminder.Skip(ClockUtc); // already skipped
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var sut = new SkipReminderCommandHandler(db, _clock);

        var result = await sut.Handle(
            new SkipReminderCommand(reminder.Id, userId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("reminders.invalid_transition");
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

        var sut = new SkipReminderCommandHandler(db, _clock);

        var result = await sut.Handle(
            new SkipReminderCommand(reminder.Id, attacker),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Forbidden);

        var persisted = await db.Reminders.FirstAsync(r => r.Id == reminder.Id);
        persisted.Status.Should().Be(ReminderStatus.Sent,
            because: "ownership check must reject before touching the entity");
    }

    [Fact]
    public async Task HandleUnknownReminderReturnsNotFound()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var sut = new SkipReminderCommandHandler(db, _clock);

        var result = await sut.Handle(
            new SkipReminderCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
