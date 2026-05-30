using FluentAssertions;

using Hangfire;

using MediatR;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Features.SkipReminder;
using Memoria.Reminders.Options;
using Memoria.Reminders.Persistence;
using Memoria.Reminders.Services;
using Memoria.Reminders.UnitTests.Infrastructure;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Memoria.Reminders.UnitTests.Features.SkipReminder;

public sealed class SkipReminderCommandHandlerTests
{
    private static readonly DateTime ClockUtc = new(2026, 5, 26, 12, 0, 0, DateTimeKind.Utc);
    private const int SampleMessageId = 42;

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(ClockUtc, TimeSpan.Zero));
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IBackgroundJobClient _hangfire = Substitute.For<IBackgroundJobClient>();
    private readonly FakeLogger<SkipReminderCommandHandler> _logger = new();

    private static ReminderScheduler CreateScheduler() =>
        new(Microsoft.Extensions.Options.Options.Create(new RemindersOptions
        {
            Intervals = new[] { TimeSpan.FromMinutes(10), TimeSpan.FromHours(1), TimeSpan.FromDays(1) },
            HardRetryInterval = TimeSpan.FromHours(1),
        }));

    private SkipReminderCommandHandler CreateSut(RemindersDbContext db) =>
        new(db, CreateScheduler(), _mediator, _hangfire,
            new DueRemindersDispatcher(db, _hangfire, NullLogger<DueRemindersDispatcher>.Instance),
            _clock, _logger);

    private void StubPrefs(Guid userId)
    {
        _mediator
            .Send(Arg.Any<GetUserPreferencesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result<UserPreferencesDto>.Success(
                new UserPreferencesDto(userId, "UTC", null, null)));
    }

    private static Reminder NewSentReminder(Guid userId, int stage = 1)
    {
        var r = new Reminder(Guid.NewGuid(), userId, stageNumber: stage, ClockUtc);
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
        StubPrefs(userId);

        var sut = CreateSut(db);

        var result = await sut.Handle(
            new SkipReminderCommand(reminder.Id, userId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await db.Reminders.FirstAsync(r => r.Id == reminder.Id);
        persisted.Status.Should().Be(ReminderStatus.Skipped);
        persisted.ConfirmedAt.Should().Be(ClockUtc);
    }

    [Fact]
    public async Task HandleSuccessfulSkipArmsRetryAtSameStage()
    {
        await using var db = RemindersDbContextTestFactory.Create();
        var userId = Guid.NewGuid();
        var reminder = NewSentReminder(userId, stage: 3);
        db.Reminders.Add(reminder);
        await db.SaveChangesAsync();
        StubPrefs(userId);

        var sut = CreateSut(db);

        var result = await sut.Handle(
            new SkipReminderCommand(reminder.Id, userId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var pending = await db.Reminders
            .Where(r => r.Status == ReminderStatus.Pending)
            .ToListAsync();
        pending.Should().ContainSingle();
        pending[0].StageNumber.Should().Be(3, because: "skip keeps the card at its current stage");
        pending[0].CardId.Should().Be(reminder.CardId);
        pending[0].ScheduledAt.Should().Be(ClockUtc.AddHours(1), because: "HardRetryInterval");
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

        var sut = CreateSut(db);

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

        var sut = CreateSut(db);

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
        var sut = CreateSut(db);

        var result = await sut.Handle(
            new SkipReminderCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }
}
