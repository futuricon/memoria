using FluentAssertions;
using Memoria.Reminders.Domain;

namespace Memoria.Reminders.UnitTests.Domain;

public sealed class ReminderTests
{
    private static readonly DateTime ClockUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private const int SampleMessageId = 42;

    private static Reminder NewPendingReminder() =>
        new(Guid.NewGuid(), Guid.NewGuid(), stageNumber: 1, ClockUtc);

    /// <summary>
    /// Walks a fresh <see cref="Reminder"/> through legal transitions until it
    /// reaches <paramref name="target"/>. Used to arrange entities in non-Pending
    /// states for illegal-source tests without resorting to reflection.
    /// </summary>
    private static Reminder ArrangeReminderInStatus(ReminderStatus target)
    {
        var r = NewPendingReminder();
        switch (target)
        {
            case ReminderStatus.Pending:
                return r;
            case ReminderStatus.Sending:
                r.BeginSending();
                return r;
            case ReminderStatus.Sent:
                r.BeginSending();
                r.MarkSent(SampleMessageId, ClockUtc);
                return r;
            case ReminderStatus.Confirmed:
                r.BeginSending();
                r.MarkSent(SampleMessageId, ClockUtc);
                r.Confirm(ClockUtc);
                return r;
            case ReminderStatus.Skipped:
                r.BeginSending();
                r.MarkSent(SampleMessageId, ClockUtc);
                r.Skip(ClockUtc);
                return r;
            case ReminderStatus.Failed:
                r.BeginSending();
                r.MarkFailed(ClockUtc);
                return r;
            case ReminderStatus.Cancelled:
                r.Cancel(ClockUtc);
                return r;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown status.");
        }
    }

    [Fact]
    public void CtorAssignsAllFieldsAndSetsStatusToPending()
    {
        var cardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var scheduledAt = new DateTime(2026, 5, 25, 12, 0, 0, DateTimeKind.Utc);

        var sut = new Reminder(cardId, userId, stageNumber: 3, scheduledAt);

        sut.Id.Should().NotBe(Guid.Empty);
        sut.CardId.Should().Be(cardId);
        sut.UserId.Should().Be(userId);
        sut.StageNumber.Should().Be(3);
        sut.ScheduledAt.Should().Be(scheduledAt);
        sut.Status.Should().Be(ReminderStatus.Pending);
        sut.SentAt.Should().BeNull();
        sut.ConfirmedAt.Should().BeNull();
        sut.HangfireJobId.Should().BeNull();
        sut.MessageId.Should().BeNull();
    }

    [Fact]
    public void AttachHangfireJobSetsIdAndKeepsPendingStatus()
    {
        var sut = NewPendingReminder();

        sut.AttachHangfireJob("job-id");

        sut.HangfireJobId.Should().Be("job-id");
        sut.Status.Should().Be(ReminderStatus.Pending);
    }

    [Fact]
    public void BeginSendingFromPendingTransitionsToSending()
    {
        var sut = NewPendingReminder();

        sut.BeginSending();

        sut.Status.Should().Be(ReminderStatus.Sending);
    }


    [Theory]
    [InlineData(ReminderStatus.Sending)]
    [InlineData(ReminderStatus.Sent)]
    [InlineData(ReminderStatus.Confirmed)]
    [InlineData(ReminderStatus.Skipped)]
    [InlineData(ReminderStatus.Failed)]
    [InlineData(ReminderStatus.Cancelled)]
    public void BeginSendingFromAnyOtherStatusThrows(ReminderStatus initial)
    {
        var sut = ArrangeReminderInStatus(initial);

        Action act = sut.BeginSending;

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{initial}*");
    }

    [Fact]
    public void MarkSentFromSendingSetsSentAtAndMessageId()
    {
        var sut = ArrangeReminderInStatus(ReminderStatus.Sending);

        sut.MarkSent(SampleMessageId, ClockUtc);

        sut.Status.Should().Be(ReminderStatus.Sent);
        sut.SentAt.Should().Be(ClockUtc);
        sut.MessageId.Should().Be(SampleMessageId);
    }

    [Theory]
    [InlineData(ReminderStatus.Pending)]
    [InlineData(ReminderStatus.Sent)]
    [InlineData(ReminderStatus.Confirmed)]
    [InlineData(ReminderStatus.Skipped)]
    [InlineData(ReminderStatus.Failed)]
    [InlineData(ReminderStatus.Cancelled)]
    public void MarkSentFromNonSendingThrows(ReminderStatus initial)
    {
        var sut = ArrangeReminderInStatus(initial);

        Action act = () => sut.MarkSent(SampleMessageId, ClockUtc);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ConfirmFromSentSetsConfirmedAt()
    {
        var sut = ArrangeReminderInStatus(ReminderStatus.Sent);

        sut.Confirm(ClockUtc);

        sut.Status.Should().Be(ReminderStatus.Confirmed);
        sut.ConfirmedAt.Should().Be(ClockUtc);
    }

    [Theory]
    [InlineData(ReminderStatus.Pending)]
    [InlineData(ReminderStatus.Sending)]
    [InlineData(ReminderStatus.Confirmed)]
    [InlineData(ReminderStatus.Skipped)]
    [InlineData(ReminderStatus.Failed)]
    [InlineData(ReminderStatus.Cancelled)]
    public void ConfirmFromNonSentThrows(ReminderStatus initial)
    {
        var sut = ArrangeReminderInStatus(initial);

        Action act = () => sut.Confirm(ClockUtc);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SkipFromSentSetsConfirmedAt()
    {
        var sut = ArrangeReminderInStatus(ReminderStatus.Sent);

        sut.Skip(ClockUtc);

        sut.Status.Should().Be(ReminderStatus.Skipped);
        sut.ConfirmedAt.Should().Be(ClockUtc);
    }

    [Theory]
    [InlineData(ReminderStatus.Pending)]
    [InlineData(ReminderStatus.Sending)]
    [InlineData(ReminderStatus.Confirmed)]
    [InlineData(ReminderStatus.Skipped)]
    [InlineData(ReminderStatus.Failed)]
    [InlineData(ReminderStatus.Cancelled)]
    public void SkipFromNonSentThrows(ReminderStatus initial)
    {
        var sut = ArrangeReminderInStatus(initial);

        Action act = () => sut.Skip(ClockUtc);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkFailedFromSendingTransitionsToFailed()
    {
        var sut = ArrangeReminderInStatus(ReminderStatus.Sending);

        sut.MarkFailed(ClockUtc);

        sut.Status.Should().Be(ReminderStatus.Failed);
        sut.ConfirmedAt.Should().Be(ClockUtc);
    }

    [Theory]
    [InlineData(ReminderStatus.Pending)]
    [InlineData(ReminderStatus.Sent)]
    [InlineData(ReminderStatus.Confirmed)]
    [InlineData(ReminderStatus.Skipped)]
    [InlineData(ReminderStatus.Failed)]
    [InlineData(ReminderStatus.Cancelled)]
    public void MarkFailedFromNonSendingThrows(ReminderStatus initial)
    {
        var sut = ArrangeReminderInStatus(initial);

        Action act = () => sut.MarkFailed(ClockUtc);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CancelFromPendingTransitionsToCancelled()
    {
        var sut = NewPendingReminder();

        sut.Cancel(ClockUtc);

        sut.Status.Should().Be(ReminderStatus.Cancelled);
        sut.ConfirmedAt.Should().Be(ClockUtc);
    }

    [Fact]
    public void CancelFromSendingTransitionsToCancelled()
    {
        var sut = ArrangeReminderInStatus(ReminderStatus.Sending);

        sut.Cancel(ClockUtc);

        sut.Status.Should().Be(ReminderStatus.Cancelled);
        sut.ConfirmedAt.Should().Be(ClockUtc);
    }

    [Theory]
    [InlineData(ReminderStatus.Sent)]
    [InlineData(ReminderStatus.Confirmed)]
    [InlineData(ReminderStatus.Skipped)]
    [InlineData(ReminderStatus.Failed)]
    [InlineData(ReminderStatus.Cancelled)]
    public void CancelFromTerminalStatusesThrows(ReminderStatus initial)
    {
        var sut = ArrangeReminderInStatus(initial);

        Action act = () => sut.Cancel(ClockUtc);

        act.Should().Throw<InvalidOperationException>();
    }
}