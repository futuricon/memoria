using FluentAssertions;

using Hangfire;

using MediatR;

using Memoria.Reminders.Domain;
using Memoria.Reminders.Features.ReviewSubscribers;
using Memoria.Reminders.Options;
using Memoria.Reminders.Persistence;
using Memoria.Reminders.Services;
using Memoria.Reminders.UnitTests.Infrastructure;
using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Events;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Testing;

using NSubstitute;

namespace Memoria.Reminders.UnitTests.Features.ReviewSubscribers;

public sealed class ReviewRecordedEventHandlerTests
{
    private static readonly DateTime ReviewedAt = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IBackgroundJobClient _hangfire = Substitute.For<IBackgroundJobClient>();
    private readonly FakeLogger<ReviewRecordedEventHandler> _logger = new();

    private static ReminderScheduler CreateScheduler() =>
        new(Microsoft.Extensions.Options.Options.Create(new RemindersOptions
        {
            Intervals = new[]
            {
                TimeSpan.FromMinutes(10),
                TimeSpan.FromHours(1),
                TimeSpan.FromDays(1),
                TimeSpan.FromDays(3),
            },
            HardRetryInterval = TimeSpan.FromHours(1),
        }));

    private ReviewRecordedEventHandler CreateSut(RemindersDbContext db) =>
        new(db, CreateScheduler(), _mediator, _hangfire, _logger);

    private void StubPrefs(Guid userId)
    {
        _mediator
            .Send(Arg.Any<GetUserPreferencesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserPreferencesDto>.Success(
                new UserPreferencesDto(userId, "UTC", null, null)));
    }

    private static Reminder ConfirmedReminder(Guid cardId, Guid userId, int stage)
    {
        var r = new Reminder(cardId, userId, stageNumber: stage, ReviewedAt.AddDays(-1));
        r.BeginSending();
        r.MarkSent(1, ReviewedAt.AddDays(-1));
        r.Confirm(ReviewedAt);
        return r;
    }

    [Fact]
    public async Task HandleWithNullReminderIdIsNoOp()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var sut = CreateSut(db);

        await sut.Handle(
            new ReviewRecordedEvent(Guid.NewGuid(), Guid.NewGuid(), ReminderId: null, Rating.Good, ReviewedAt),
            CancellationToken.None);

        (await db.Reminders.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task HandleGoodRatingArmsNextStage()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var cardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reviewed = ConfirmedReminder(cardId, userId, stage: 2);
        db.Reminders.Add(reviewed);
        await db.SaveChangesAsync();
        StubPrefs(userId);

        var sut = CreateSut(db);

        await sut.Handle(
            new ReviewRecordedEvent(cardId, userId, reviewed.Id, Rating.Good, ReviewedAt),
            CancellationToken.None);

        var pending = await db.Reminders
            .Where(r => r.Status == ReminderStatus.Pending)
            .ToListAsync();
        pending.Should().ContainSingle();
        pending[0].StageNumber.Should().Be(3, because: "Good advances one stage");
        pending[0].ScheduledAt.Should().Be(ReviewedAt.AddDays(1), because: "Intervals[2]");
    }

    [Fact]
    public async Task HandleForgotRatingResetsToStageOne()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var cardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reviewed = ConfirmedReminder(cardId, userId, stage: 4);
        db.Reminders.Add(reviewed);
        await db.SaveChangesAsync();
        StubPrefs(userId);

        var sut = CreateSut(db);

        await sut.Handle(
            new ReviewRecordedEvent(cardId, userId, reviewed.Id, Rating.Forgot, ReviewedAt),
            CancellationToken.None);

        var pending = await db.Reminders.SingleAsync(r => r.Status == ReminderStatus.Pending);
        pending.StageNumber.Should().Be(1);
        pending.ScheduledAt.Should().Be(ReviewedAt.AddMinutes(10));
    }

    [Fact]
    public async Task HandleMismatchedReminderDoesNotReschedule()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var cardId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reviewed = ConfirmedReminder(cardId, userId, stage: 2);
        db.Reminders.Add(reviewed);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);

        // Event references the reminder but with a different (attacker) user id.
        await sut.Handle(
            new ReviewRecordedEvent(cardId, Guid.NewGuid(), reviewed.Id, Rating.Good, ReviewedAt),
            CancellationToken.None);

        (await db.Reminders.CountAsync(r => r.Status == ReminderStatus.Pending)).Should().Be(0);
    }
}
