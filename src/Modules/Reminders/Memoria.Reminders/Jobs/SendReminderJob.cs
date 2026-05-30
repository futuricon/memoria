using MediatR;

using Memoria.Cards.Contracts.Queries;
using Memoria.Reminders.Contracts.Abstractions;
using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoria.Reminders.Jobs;

/// <summary>
/// Hangfire job that delivers a single <see cref="Reminder"/> via the
/// <see cref="IReminderNotificationSender"/> port. Idempotent on non-Pending
/// status. Cancels the reminder if the card is soft-deleted at execution time.
/// </summary>
internal sealed class SendReminderJob
{
    private readonly RemindersDbContext _db;
    private readonly IMediator _mediator;
    private readonly IReminderNotificationSender _sender;
    private readonly TimeProvider _clock;
    private readonly ILogger<SendReminderJob> _logger;

    public SendReminderJob(
        RemindersDbContext db,
        IMediator mediator,
        IReminderNotificationSender sender,
        TimeProvider clock,
        ILogger<SendReminderJob> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _mediator = mediator;
        _sender = sender;
        _clock = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid reminderId, CancellationToken ct)
    {
        var reminder = await _db.Reminders
            .FirstOrDefaultAsync(r => r.Id == reminderId, ct)
            .ConfigureAwait(false);

        if (reminder is null)
        {
            _logger.LogWarning("SendReminderJob: reminder {ReminderId} not found, skipping", reminderId);
            return;
        }

        if (reminder.Status != ReminderStatus.Pending)
        {
            _logger.LogWarning(
                "SendReminderJob: reminder {ReminderId} already in status {Status}, skipping",
                reminderId, reminder.Status);
            return;
        }

        // Serialization: at most one reminder in-flight per user. If another is
        // already Sent (awaiting the user's response), this one stays Pending and
        // gets picked up by DueRemindersDispatcher when the in-flight resolves.
        var anotherInFlight = await _db.Reminders
            .AnyAsync(r => r.UserId == reminder.UserId
                           && r.Status == ReminderStatus.Sent
                           && r.Id != reminderId, ct)
            .ConfigureAwait(false);
        if (anotherInFlight)
        {
            _logger.LogInformation(
                "SendReminderJob: reminder {ReminderId} deferred — user {UserId} has another reminder in flight",
                reminderId, reminder.UserId);
            return;
        }

        var now = _clock.GetUtcNow().UtcDateTime;

        // Re-check card. With IncludeDeleted: false, soft-deleted or missing cards
        // surface as Failure → we treat both as "cancel reminder" per addendum §6.1.
        var cardResult = await _mediator
            .Send(new GetCardByIdQuery(reminder.UserId, reminder.CardId, IncludeDeleted: false), ct)
            .ConfigureAwait(false);

        if (cardResult.IsFailure)
        {
            _logger.LogWarning(
                "SendReminderJob: card {CardId} unavailable ({ErrorCode}) — cancelling reminder {ReminderId}",
                reminder.CardId, cardResult.Error!.Code, reminderId);
            reminder.Cancel(now);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return;
        }

        var card = cardResult.Value!;

        reminder.BeginSending();
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        var notification = new ReminderNotification(
            ReminderId: reminder.Id,
            UserId: reminder.UserId,
            CardId: reminder.CardId,
            CardTitle: card.Title,
            CardBody: card.Body,
            Tags: card.Tags,
            StageNumber: reminder.StageNumber,
            CardType: card.Type);

        var sendResult = await _sender.SendReminderAsync(notification, ct).ConfigureAwait(false);

        if (sendResult.IsSuccess)
        {
            _logger.LogInformation(
                "SendReminderJob: reminder {ReminderId} delivered, messageId={MessageId}",
                reminderId, sendResult.Value);
            await _mediator
                .Send(new MarkReminderSentCommand(reminderId, sendResult.Value), ct)
                .ConfigureAwait(false);
        }
        else
        {
            _logger.LogError(
                "SendReminderJob: reminder {ReminderId} delivery failed: {ErrorCode} — {ErrorMessage}",
                reminderId, sendResult.Error!.Code, sendResult.Error.Message);
            await _mediator
                .Send(new MarkReminderFailedCommand(reminderId, sendResult.Error.Code), ct)
                .ConfigureAwait(false);
        }
    }
}
