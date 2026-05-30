using FluentAssertions;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Features.ReassignRemindersOwner;
using Memoria.Reminders.UnitTests.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reminders.UnitTests.Features.ReassignRemindersOwner;

public sealed class ReassignRemindersOwnerCommandHandlerTests
{
    private static readonly DateTime ScheduledAt = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleMovesAllSourceRemindersToTarget()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        var card = Guid.NewGuid();
        db.Reminders.AddRange(
            new Reminder(card, source, stageNumber: 1, ScheduledAt),
            new Reminder(card, source, stageNumber: 2, ScheduledAt),
            new Reminder(card, Guid.NewGuid(), stageNumber: 1, ScheduledAt)); // unrelated user
        await db.SaveChangesAsync();

        var sut = new ReassignRemindersOwnerCommandHandler(db);
        var result = await sut.Handle(
            new ReassignRemindersOwnerCommand(source, target), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        (await db.Reminders.CountAsync(r => r.UserId == source)).Should().Be(0);
        (await db.Reminders.CountAsync(r => r.UserId == target)).Should().Be(2);
    }

    [Fact]
    public async Task HandleIsIdempotentOnRerun()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();
        db.Reminders.Add(new Reminder(Guid.NewGuid(), source, 1, ScheduledAt));
        await db.SaveChangesAsync();

        var sut = new ReassignRemindersOwnerCommandHandler(db);
        await sut.Handle(new ReassignRemindersOwnerCommand(source, target), CancellationToken.None);
        var second = await sut.Handle(
            new ReassignRemindersOwnerCommand(source, target), CancellationToken.None);

        second.IsSuccess.Should().BeTrue();
        second.Value.Should().Be(0, "second pass finds no rows owned by source");
    }

    [Fact]
    public async Task HandleWhenSourceEqualsTargetReturnsZero()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var user = Guid.NewGuid();

        var result = await new ReassignRemindersOwnerCommandHandler(db).Handle(
            new ReassignRemindersOwnerCommand(user, user), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }
}
