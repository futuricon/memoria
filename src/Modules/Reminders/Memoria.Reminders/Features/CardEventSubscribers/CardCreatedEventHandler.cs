using MediatR;

using Memoria.Cards.Contracts.Events;
using Memoria.Reminders.Contracts.Commands;

using Microsoft.Extensions.Logging;

namespace Memoria.Reminders.Features.CardEventSubscribers;

/// <summary>
/// Subscribes to <see cref="CardCreatedEvent"/> from the Cards module and
/// dispatches <see cref="ScheduleRemindersForCardCommand"/> so the Reminders
/// module builds the Ebbinghaus schedule for the new card. Failures are
/// logged but never rethrown — notifications must not break the publisher.
/// </summary>
internal sealed class CardCreatedEventHandler : INotificationHandler<CardCreatedEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<CardCreatedEventHandler> _logger;

    public CardCreatedEventHandler(IMediator mediator, ILogger<CardCreatedEventHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(logger);
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(CardCreatedEvent notification, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var result = await _mediator.Send(
            new ScheduleRemindersForCardCommand(
                notification.CardId,
                notification.UserId,
                notification.CreatedAt),
            cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            _logger.LogWarning(
                "CardCreatedEventHandler: failed to schedule reminders for card {CardId}: {ErrorCode} — {ErrorMessage}",
                notification.CardId,
                result.Error!.Code,
                result.Error.Message);
        }
    }
}
