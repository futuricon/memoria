using FluentAssertions;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Features.MarkReminderSent;
using Memoria.Reminders.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Memoria.Reminders.UnitTests.Features.MarkReminderSent;

public sealed class MarkReminderSentCommandHandlerTests
{
    private static readonly DateTime ClockUtc = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
    private const int SampleMessageId = 42;

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(ClockUtc, TimeSpan.Zero));

    private static Reminder NewPendingReminder() =>
        new(Guid.NewGuid(), Guid.NewGuid(), stageNumber: 1, ClockUtc);

    [Fact]
    public async Task HandleSendingReminderTransitionsToSent()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var reminder = NewPendingReminder();
        reminder.BeginSending();
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var sut = new MarkReminderSentCommandHandler(db, _clock);

        var result = await sut.Handle(
            new MarkReminderSentCommand(reminder.Id, SampleMessageId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await db.Reminders.FirstAsync(r => r.Id == reminder.Id);
        persisted.Status.Should().Be(ReminderStatus.Sent);
        persisted.SentAt.Should().Be(ClockUtc);
        persisted.MessageId.Should().Be(SampleMessageId);
    }

    [Fact]
    public async Task HandleUnknownReminderReturnsNotFound()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var sut = new MarkReminderSentCommandHandler(db, _clock);

        var result = await sut.Handle(
            new MarkReminderSentCommand(Guid.NewGuid(), SampleMessageId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task HandleReminderInWrongStatusReturnsConflict()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var reminder = NewPendingReminder(); // status = Pending
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();

        var sut = new MarkReminderSentCommandHandler(db, _clock);

        var result = await sut.Handle(
            new MarkReminderSentCommand(reminder.Id, SampleMessageId),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("reminders.invalid_transition");

        var persisted = await db.Reminders.FirstAsync(r => r.Id == reminder.Id);
        persisted.Status.Should().Be(ReminderStatus.Pending);
    }
}
