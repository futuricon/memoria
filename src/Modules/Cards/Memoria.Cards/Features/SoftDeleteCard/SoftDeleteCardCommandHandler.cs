using MediatR;
using Microsoft.EntityFrameworkCore;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Contracts.Events;
using Memoria.Cards.Persistence;
using Memoria.Shared.Kernel.Results;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Cards.Features.SoftDeleteCard;

internal sealed class SoftDeleteCardCommandHandler : IRequestHandler<SoftDeleteCardCommand, Result<Unit>>
{
    private readonly CardsDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IPublisher _publisher;

    public SoftDeleteCardCommandHandler(CardsDbContext db, TimeProvider clock, IPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(publisher);
        _db = db;
        _clock = clock;
        _publisher = publisher;
    }

    public async Task<Result<Unit>> Handle(SoftDeleteCardCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var card = await _db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
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

        var now = _clock.GetUtcNow().UtcDateTime;
        card.SoftDelete(now);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _publisher.Publish(new CardSoftDeletedEvent(card.Id, card.UserId, now), cancellationToken)
            .ConfigureAwait(false);

        return Result<Unit>.Success(Unit.Value);
    }
}
