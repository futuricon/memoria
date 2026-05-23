using MediatR;
using Microsoft.EntityFrameworkCore;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Events;
using Memoria.Cards.Persistence;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Features.RestoreCard;

internal sealed class RestoreCardCommandHandler : IRequestHandler<RestoreCardCommand, Result<CardDto>>
{
    private const int RetentionDays = 90;

    private readonly CardsDbContext _db;
    private readonly TimeProvider _clock;
    private readonly IPublisher _publisher;

    public RestoreCardCommandHandler(CardsDbContext db, TimeProvider clock, IPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(publisher);
        _db = db;
        _clock = clock;
        _publisher = publisher;
    }

    public async Task<Result<CardDto>> Handle(RestoreCardCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var card = await _db.Cards
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            .ConfigureAwait(false);

        if (card is null)
        {
            return Result<CardDto>.Failure(Error.NotFound("cards.not_found", "Card not found."));
        }

        if (card.UserId != request.UserId)
        {
            return Result<CardDto>.Failure(Error.Forbidden(
                "cards.not_owner", "Card belongs to another user."));
        }

        if (card.DeletedAt is null)
        {
            return Result<CardDto>.Failure(Error.Conflict(
                "cards.not_deleted", "Card is already active."));
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        if (card.DeletedAt.Value < now.AddDays(-RetentionDays))
        {
            return Result<CardDto>.Failure(Error.NotFound(
                "cards.retention_expired", "Card retention period has expired."));
        }

        card.Restore(now);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _publisher.Publish(new CardRestoredEvent(card.Id, card.UserId, now), cancellationToken)
            .ConfigureAwait(false);

        var tags = await _db.LoadTagsForCardAsync(card.Id, cancellationToken).ConfigureAwait(false);
        return CardQueries.ToDto(card, tags);
    }
}
