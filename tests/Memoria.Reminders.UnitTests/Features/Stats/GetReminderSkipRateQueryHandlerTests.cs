using FluentAssertions;

using Memoria.Reminders.Contracts.Queries;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Features.Stats;
using Memoria.Reminders.UnitTests.Infrastructure;

using Microsoft.Extensions.Time.Testing;

namespace Memoria.Reminders.UnitTests.Features.Stats;

public sealed class GetReminderSkipRateQueryHandlerTests
{
    private readonly FakeTimeProvider _clock =
        new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));

    private static Reminder At(DateTime when)
    {
        var r = new Reminder(Guid.NewGuid(), Guid.NewGuid(), stageNumber: 0, scheduledAt: when);
        return r;
    }

    [Fact]
    public async Task HandleCountsByTerminalStatusWithinWindow()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var now = _clock.GetUtcNow().UtcDateTime;

        var confirmed = At(now);
        confirmed.BeginSending();
        confirmed.MarkSent(messageId: 1, now);
        confirmed.Confirm(now);

        var sent = At(now.AddDays(-2));
        sent.BeginSending();
        sent.MarkSent(messageId: 2, now.AddDays(-2));

        var skipped = At(now.AddDays(-3));
        skipped.BeginSending();
        skipped.MarkSent(messageId: 3, now.AddDays(-3));
        skipped.Skip(now.AddDays(-3));

        var failed = At(now.AddDays(-4));
        failed.BeginSending();
        failed.MarkFailed(now.AddDays(-4));

        var pending = At(now.AddDays(-5));
        // Pending must be excluded

        var oldConfirmed = At(now.AddDays(-40));
        oldConfirmed.BeginSending();
        oldConfirmed.MarkSent(messageId: 5, now.AddDays(-40));
        oldConfirmed.Confirm(now.AddDays(-40));

        db.Reminders.AddRange(confirmed, sent, skipped, failed, pending, oldConfirmed);
        await db.SaveChangesAsync();

        var sut = new GetReminderSkipRateQueryHandler(db, _clock);
        var result = await sut.Handle(new GetReminderSkipRateQuery(30), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Confirmed.Should().Be(1);
        result.Value.Sent.Should().Be(1);
        result.Value.Skipped.Should().Be(1);
        result.Value.Failed.Should().Be(1);
        result.Value.Total.Should().Be(4);
    }
}
