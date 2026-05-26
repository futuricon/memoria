using MediatR;

using Memoria.Cards.Contracts.Events;
using Memoria.Reminders.Contracts.Commands;

using Microsoft.Extensions.Logging;

namespace Memoria.Reminders.Features.CardEventSubscribers;

/// <summary>
/// Subscribes to <see cref="CardSoftDeletedEvent"/> and cancels pending
/// reminders + their Hangfire jobs by dispatching
/// <see cref="CancelRemindersForCardCommand"/>.
/// Per addendum §2: pending reminders are hard-deleted, sent ones stay.
/// Failures are logged, never rethrown — notifications must not break the publisher.
/// </summary>
internal sealed class CardSoftDeletedEventHandler : INotificationHandler<CardSoftDeletedEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<CardSoftDeletedEventHandler> _logger;

    public CardSoftDeletedEventHandler(IMediator mediator, ILogger<CardSoftDeletedEventHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(logger);
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(CardSoftDeletedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var result = await _mediator
            .Send(new CancelRemindersForCardCommand(notification.CardId), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            _logger.LogWarning(
                "CardSoftDeletedEventHandler: failed to cancel reminders for card {CardId}: {ErrorCode} — {ErrorMessage}",
                notification.CardId,
                result.Error!.Code,
                result.Error.Message);
        }
    }
}
