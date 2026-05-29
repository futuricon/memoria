using Hangfire;

using MediatR;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Jobs;
using Memoria.Reminders.Persistence;
using Memoria.Reminders.Services;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Queries;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Reminders.Features.SkipReminder;

internal sealed class SkipReminderCommandHandler
    : IRequestHandler<SkipReminderCommand, Result<Unit>>
{
    private readonly RemindersDbContext _db;
    private readonly ReminderScheduler _scheduler;
    private readonly IMediator _mediator;
    private readonly IBackgroundJobClient _hangfire;
    private readonly TimeProvider _clock;
    private readonly ILogger<SkipReminderCommandHandler> _logger;

    public SkipReminderCommandHandler(
        RemindersDbContext db,
        ReminderScheduler scheduler,
        IMediator mediator,
        IBackgroundJobClient hangfire,
        TimeProvider clock,
        ILogger<SkipReminderCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(hangfire);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _scheduler = scheduler;
        _mediator = mediator;
        _hangfire = hangfire;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<Unit>> Handle(SkipReminderCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reminder = await _db.Reminders
            .FirstOrDefaultAsync(r => r.Id == request.ReminderId, ct)
            .ConfigureAwait(false);

        if (reminder is null)
        {
            return Result<Unit>.Failure(Error.NotFound(
                "reminders.not_found", "Reminder not found."));
        }

        if (reminder.UserId != request.UserId)
        {
            return Result<Unit>.Failure(Error.Forbidden(
                "reminders.not_owner", "Reminder belongs to another user."));
        }

        var now = _clock.GetUtcNow().UtcDateTime;

        try
        {
            reminder.Skip(now);
        }
        catch (InvalidOperationException ex)
        {
            return Result<Unit>.Failure(Error.Conflict(
                "reminders.invalid_transition", ex.Message));
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Skip keeps the card at its current stage but re-arms it sooner
        // (HardRetryInterval). Failure to reschedule must not fail the skip.
        var prefsResult = await _mediator
            .Send(new GetUserPreferencesQuery(request.UserId), ct)
            .ConfigureAwait(false);

        if (prefsResult.IsFailure)
        {
            _logger.LogWarning(
                "SkipReminderCommandHandler: reminder {ReminderId} skipped, but cannot load preferences: {Code} — {Message}",
                reminder.Id, prefsResult.Error!.Code, prefsResult.Error.Message);
            return Result<Unit>.Success(Unit.Value);
        }

        var retry = _scheduler.CreateRetryReminder(
            reminder.CardId, reminder.UserId, reminder.StageNumber, prefsResult.Value!, now);

        _db.Reminders.Add(retry);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        var retryId = retry.Id;
        var jobId = _hangfire.Schedule<SendReminderJob>(
            job => job.ExecuteAsync(retryId, CancellationToken.None),
            new DateTimeOffset(retry.ScheduledAt, TimeSpan.Zero));
        retry.AttachHangfireJob(jobId);

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<Unit>.Success(Unit.Value);
    }
}
