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

        var items = await _db.Tags
            .Where(t => t.UserId == request.UserId)
            .Select(t => new TagDto(
                t.Id,
                t.NormalizedName,
                _db.CardTags.Count(ct => ct.TagId == t.Id
                                         && _db.Cards.Any(c => c.Id == ct.CardId))))
            .Where(x => x.CardCount > 0)
            .OrderByDescending(x => x.CardCount)
            .ThenBy(x => x.Name)
            .Take(Math.Clamp(request.Count, 1, 50))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<TagDto>>.Success(items);
    }
}