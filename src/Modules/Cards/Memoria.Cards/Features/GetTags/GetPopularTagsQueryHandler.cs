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

        var items = await (
                from ct in _db.CardTags
                join t in _db.Tags on ct.TagId equals t.Id
                join c in _db.Cards on ct.CardId equals c.Id
                where c.UserId == request.UserId
                      && c.DeletedAt == null
                group ct by new
                {
                    t.Id,
                    t.NormalizedName
                }
                into g
                orderby g.Count() descending
                select new TagDto(
                
                    g.Key.Id,
                    g.Key.NormalizedName,
                    g.Count()
                )
            )
            .Take(request.Count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<TagDto>>.Success(items);
    }
}