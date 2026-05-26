using MediatR;

using Memoria.Cards.Contracts.Events;
using Memoria.Reminders.Contracts.Commands;

using Microsoft.Extensions.Logging;

namespace Memoria.Reminders.Features.CardEventSubscribers;

/// <summary>
/// Subscribes to <see cref="CardRestoredEvent"/>. Cancels any leftover
/// reminders for the card and creates a fresh Ebbinghaus schedule anchored
/// at <see cref="CardRestoredEvent.RestoredAt"/> — NOT the original creation
/// time, per addendum §4 step 5. Failures are logged, never rethrown.
/// </summary>
internal sealed class CardRestoredEventHandler : INotificationHandler<CardRestoredEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<CardRestoredEventHandler> _logger;

    public CardRestoredEventHandler(IMediator mediator, ILogger<CardRestoredEventHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(logger);
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(CardRestoredEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var cancelResult = await _mediator
            .Send(new CancelRemindersForCardCommand(notification.CardId), cancellationToken)
            .ConfigureAwait(false);

        if (cancelResult.IsFailure)
        {
            _logger.LogWarning(
                "CardRestoredEventHandler: leftover cancel failed for card {CardId}: {ErrorCode} — {ErrorMessage}",
                notification.CardId,
                cancelResult.Error!.Code,
                cancelResult.Error.Message);
        }

        var scheduleResult = await _mediator
            .Send(
                new ScheduleRemindersForCardCommand(
                    notification.CardId,
                    notification.UserId,
                    notification.RestoredAt),
                cancellationToken)
            .ConfigureAwait(false);

        if (scheduleResult.IsFailure)
        {
            _logger.LogError(
                "CardRestoredEventHandler: reschedule failed for card {CardId}: {ErrorCode} — {ErrorMessage}",
                notification.CardId,
                scheduleResult.Error!.Code,
                scheduleResult.Error.Message);
        }
    }
}
