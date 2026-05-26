using MediatR;

using Memoria.Reminders.Contracts.Abstractions;
using Memoria.Reminders.Contracts.Commands;

namespace Memoria.Reminders.Services;

/// <summary>
/// Adapter for <see cref="IRemindersScheduler"/> port. Cancels reminders by
/// delegating to <see cref="CancelRemindersForCardCommand"/> via MediatR —
/// avoids duplicating the cancellation logic in two places.
/// </summary>
internal sealed class RemindersScheduler : IRemindersScheduler
{
    private readonly IMediator _mediator;

    public RemindersScheduler(IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        _mediator = mediator;
    }

    public Task CancelForCardAsync(Guid cardId, CancellationToken ct)
    {
        return _mediator.Send(new CancelRemindersForCardCommand(cardId), ct);
    }
}
