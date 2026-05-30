using MediatR;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Persistence;
using Memoria.Reminders.Contracts.Commands;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Cards.Features.UnpauseCard;

internal sealed class UnpauseCardCommandHandler
    : IRequestHandler<UnpauseCardCommand, Result<Unit>>
{
    private readonly CardsDbContext _db;
    private readonly IMediator _mediator;
    private readonly TimeProvider _clock;

    public UnpauseCardCommandHandler(CardsDbContext db, IMediator mediator, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _mediator = mediator;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(UnpauseCardCommand request, CancellationToken ct)
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

        if (!card.IsPaused)
        {
            return Result<Unit>.Failure(Error.Conflict(
                "cards.not_paused", "Card is not paused."));
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var resumeStage = card.Unpause(now);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        var schedule = await _mediator
            .Send(new ScheduleRemindersForCardCommand(card.Id, card.UserId, now, resumeStage), ct)
            .ConfigureAwait(false);

        return schedule.IsSuccess
            ? Result<Unit>.Success(Unit.Value)
            : Result<Unit>.Failure(schedule.Error!);
    }
}
