using MediatR;
using Microsoft.EntityFrameworkCore;
using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Queries;
using Memoria.Cards.Persistence;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Features.GetTags;

internal sealed class GetPopularTagsQueryHandler : IRequestHandler<GetPopularTagsQuery, Result<IReadOnlyList<TagDto>>>
{
    private readonly CardsDbContext _db;

    public GetPopularTagsQueryHandler(CardsDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<IReadOnlyList<TagDto>>> Handle(
        GetPopularTagsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var count = Math.Clamp(request.Count, 1, 50);
    
        var query = from tag in _db.Tags
            where tag.UserId == request.UserId
            join ct in _db.CardTags on tag.Id equals ct.TagId
            join card in _db.Cards on ct.CardId equals card.Id
            where card.DeletedAt == null
            group ct by new { tag.Id, tag.NormalizedName } into g
            select new TagDto(
                g.Key.Id,
                g.Key.NormalizedName,
                g.Count()
            );

        var items = await query
            .OrderByDescending(x => x.CardCount)
            .ThenBy(x => x.Name)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<TagDto>>.Success(items);
    }
}