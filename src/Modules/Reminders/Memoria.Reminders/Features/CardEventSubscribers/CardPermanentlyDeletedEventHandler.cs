using MediatR;

using Memoria.Cards.Contracts.Events;
using Memoria.Reminders.Contracts.Commands;

using Microsoft.Extensions.Logging;

namespace Memoria.Reminders.Features.CardEventSubscribers;

/// <summary>
/// Subscribes to <see cref="CardPermanentlyDeletedEvent"/>. Safety net for
/// removing any reminders that survived the earlier soft-delete cancel —
/// usually none, but cheap to be defensive. Mirrors
/// <see cref="CardSoftDeletedEventHandler"/>.
/// </summary>
internal sealed class CardPermanentlyDeletedEventHandler
    : INotificationHandler<CardPermanentlyDeletedEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<CardPermanentlyDeletedEventHandler> _logger;

    public CardPermanentlyDeletedEventHandler(
        IMediator mediator,
        ILogger<CardPermanentlyDeletedEventHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(logger);
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(CardPermanentlyDeletedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var result = await _mediator
            .Send(new CancelRemindersForCardCommand(notification.CardId), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            _logger.LogWarning(
                "CardPermanentlyDeletedEventHandler: failed to cancel reminders for card {CardId}: {ErrorCode} — {ErrorMessage}",
                notification.CardId,
                result.Error!.Code,
                result.Error.Message);
        }
    }
}
