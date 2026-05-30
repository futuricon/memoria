using MediatR;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Persistence;
using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Contracts.Queries;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Cards.Features.PauseCard;

internal sealed class PauseCardCommandHandler
    : IRequestHandler<PauseCardCommand, Result<Unit>>
{
    private readonly CardsDbContext _db;
    private readonly IMediator _mediator;
    private readonly TimeProvider _clock;

    public PauseCardCommandHandler(CardsDbContext db, IMediator mediator, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _mediator = mediator;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(PauseCardCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var card = await _db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId, ct)
            .ConfigureAwait(false);

        if (card is null)
        {
            return Result<Unit>.Failure(Error.NotFound("cards.not_found", "Card not found."));
        }

        if (card.UserId != request.UserId)
        {
            return Result<Unit>.Failure(Error.Forbidden(
                "cards.not_owner", "Card belongs to another user."));
        }

        if (card.IsPaused)
        {
            return Result<Unit>.Failure(Error.Conflict(
                "cards.already_paused", "Card is already paused."));
        }

        // Snapshot the current stage BEFORE cancelling — once reminders are
        // gone we'd have no way to reconstruct where the user was.
        var stageResult = await _mediator
            .Send(new GetCurrentCardStageQuery(card.Id), ct)
            .ConfigureAwait(false);

        var stage = stageResult.IsSuccess ? stageResult.Value : null;

        var cancelled = await _mediator
            .Send(new CancelRemindersForCardCommand(card.Id), ct)
            .ConfigureAwait(false);

        if (cancelled.IsFailure)
        {
            return Result<Unit>.Failure(cancelled.Error!);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        card.Pause(stage, now);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Result<Unit>.Success(Unit.Value);
    }
}
