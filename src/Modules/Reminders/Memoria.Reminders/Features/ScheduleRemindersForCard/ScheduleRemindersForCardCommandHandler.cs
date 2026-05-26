using Hangfire;

using MediatR;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Jobs;
using Memoria.Reminders.Persistence;
using Memoria.Reminders.Services;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Queries;

using Microsoft.EntityFrameworkCore;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Reminders.Features.ScheduleRemindersForCard;

internal sealed class ScheduleRemindersForCardCommandHandler
    : IRequestHandler<ScheduleRemindersForCardCommand, Result<Unit>>
{
    private readonly RemindersDbContext _db;
    private readonly ReminderScheduler _scheduler;
    private readonly IMediator _mediator;
    private readonly IBackgroundJobClient _hangfire;

    public ScheduleRemindersForCardCommandHandler(
        RemindersDbContext db,
        ReminderScheduler scheduler,
        IMediator mediator,
        IBackgroundJobClient hangfire)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(hangfire);
        _db = db;
        _scheduler = scheduler;
        _mediator = mediator;
        _hangfire = hangfire;
    }

    public async Task<Result<Unit>> Handle(ScheduleRemindersForCardCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var exists = await _db.Reminders
            .AnyAsync(r => r.CardId == request.CardId, ct)
            .ConfigureAwait(false);
        if (exists)
        {
            return Result<Unit>.Success(Unit.Value);
        }

        var prefsResult = await _mediator
            .Send(new GetUserPreferencesQuery(request.UserId), ct)
            .ConfigureAwait(false);

        if (prefsResult.IsFailure)
        {
            return Result<Unit>.Failure(prefsResult.Error!);
        }

        var reminders = _scheduler.CreateScheduleFor(
            request.CardId, request.UserId, prefsResult.Value!, request.AnchorUtc);

        _db.Reminders.AddRange(reminders);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        foreach (var reminder in reminders)
        {
            var reminderId = reminder.Id;
            var jobId = _hangfire.Schedule<SendReminderJob>(
                job => job.ExecuteAsync(reminderId, CancellationToken.None),
                new DateTimeOffset(reminder.ScheduledAt, TimeSpan.Zero));

            reminder.AttachHangfireJob(jobId);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<Unit>.Success(Unit.Value);
    }
}
