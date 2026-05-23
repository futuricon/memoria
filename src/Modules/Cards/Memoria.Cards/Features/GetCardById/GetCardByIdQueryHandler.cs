using MediatR;
using Microsoft.EntityFrameworkCore;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Queries;
using Memoria.Cards.Persistence;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Features.GetCardById;

internal sealed class GetCardByIdQueryHandler : IRequestHandler<GetCardByIdQuery, Result<CardDto>>
{
    private readonly CardsDbContext _db;

    public GetCardByIdQueryHandler(CardsDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<CardDto>> Handle(GetCardByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = request.IncludeDeleted
            ? _db.Cards.IgnoreQueryFilters()
            : _db.Cards.AsQueryable();

        var card = await query
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

        var tags = await _db.LoadTagsForCardAsync(card.Id, cancellationToken).ConfigureAwait(false);
        return CardQueries.ToDto(card, tags);
    }
}
