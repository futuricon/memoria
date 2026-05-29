using Hangfire;

using MediatR;

using Memoria.Reminders.Jobs;
using Memoria.Reminders.Persistence;
using Memoria.Reminders.Services;
using Memoria.Reviews.Contracts.Events;
using Memoria.Users.Contracts.Queries;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoria.Reminders.Features.ReviewSubscribers;

/// <summary>
/// Subscribes to <see cref="ReviewRecordedEvent"/> and arms the next reminder
/// for the card per the adaptive ladder. No-op when the review was not tied to
/// a reminder. Failures are logged but never rethrown — a notification handler
/// must not break the review flow.
/// </summary>
internal sealed class ReviewRecordedEventHandler : INotificationHandler<ReviewRecordedEvent>
{
    private readonly RemindersDbContext _db;
    private readonly ReminderScheduler _scheduler;
    private readonly IMediator _mediator;
    private readonly IBackgroundJobClient _hangfire;
    private readonly ILogger<ReviewRecordedEventHandler> _logger;

    public ReviewRecordedEventHandler(
        RemindersDbContext db,
        ReminderScheduler scheduler,
        IMediator mediator,
        IBackgroundJobClient hangfire,
        ILogger<ReviewRecordedEventHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(hangfire);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _scheduler = scheduler;
        _mediator = mediator;
        _hangfire = hangfire;
        _logger = logger;
    }

    public async Task Handle(ReviewRecordedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (notification.ReminderId is not { } reminderId)
        {
            return;
        }

        var reviewed = await _db.Reminders
            .FirstOrDefaultAsync(r => r.Id == reminderId, cancellationToken)
            .ConfigureAwait(false);

        if (reviewed is null
            || reviewed.CardId != notification.CardId
            || reviewed.UserId != notification.UserId)
        {
            _logger.LogWarning(
                "ReviewRecordedEventHandler: reminder {ReminderId} missing or mismatched for card {CardId}; skipping reschedule",
                reminderId, notification.CardId);
            return;
        }

        var prefsResult = await _mediator
            .Send(new GetUserPreferencesQuery(notification.UserId), cancellationToken)
            .ConfigureAwait(false);

        if (prefsResult.IsFailure)
        {
            _logger.LogWarning(
                "ReviewRecordedEventHandler: cannot load preferences for user {UserId}: {Code} — {Message}",
                notification.UserId, prefsResult.Error!.Code, prefsResult.Error.Message);
            return;
        }

        var next = _scheduler.CreateNextReminder(
            notification.CardId,
            notification.UserId,
            reviewed.StageNumber,
            notification.Rating,
            prefsResult.Value!,
            notification.ReviewedAt);

        _db.Reminders.Add(next);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var nextId = next.Id;
        var jobId = _hangfire.Schedule<SendReminderJob>(
            job => job.ExecuteAsync(nextId, CancellationToken.None),
            new DateTimeOffset(next.ScheduledAt, TimeSpan.Zero));
        next.AttachHangfireJob(jobId);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
