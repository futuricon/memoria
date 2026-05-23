using MediatR;
using Microsoft.EntityFrameworkCore;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Contracts.Events;
using Memoria.Cards.Persistence;
using Memoria.Shared.Kernel.Results;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Cards.Features.PermanentlyDeleteCard;

internal sealed class PermanentlyDeleteCardCommandHandler : IRequestHandler<PermanentlyDeleteCardCommand, Result<Unit>>
{
    private readonly CardsDbContext _db;
    private readonly IPublisher _publisher;

    public PermanentlyDeleteCardCommandHandler(CardsDbContext db, IPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(publisher);
        _db = db;
        _publisher = publisher;
    }

    public async Task<Result<Unit>> Handle(PermanentlyDeleteCardCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var card = await _db.Cards
            .IgnoreQueryFilters()
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

        if (card.DeletedAt is null)
        {
            return Result<Unit>.Failure(Error.Conflict(
                "cards.not_in_trash", "Card must be soft-deleted before permanent deletion."));
        }

        var links = await _db.CardTags
            .Where(ct => ct.CardId == card.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        _db.CardTags.RemoveRange(links);
        _db.Cards.Remove(card);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _publisher.Publish(new CardPermanentlyDeletedEvent(card.Id, card.UserId), cancellationToken)
            .ConfigureAwait(false);

        return Result<Unit>.Success(Unit.Value);
    }
}
