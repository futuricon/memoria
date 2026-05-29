using MediatR;
using Microsoft.EntityFrameworkCore;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Queries;
using Memoria.Cards.Persistence;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Features.ListCards;

internal sealed class ListCardsQueryHandler : IRequestHandler<ListCardsQuery, Result<PagedResult<CardSummaryDto>>>
{
    private const int MaxPageSize = 100;
    private readonly CardsDbContext _db;

    public ListCardsQueryHandler(CardsDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<PagedResult<CardSummaryDto>>> Handle(
        ListCardsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var query = _db.Cards.Where(c => c.UserId == request.UserId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(c => EF.Functions.ILike(c.Title, pattern));
        }

        if (request.Tags is { Count: > 0 })
        {
            foreach (var tag in request.Tags.Distinct())
            {
                var t = tag;
                query = query.Where(c => _db.CardTags
                    .Any(ct => ct.CardId == c.Id
                               && _db.Tags.Any(tg => tg.Id == ct.TagId
                                                     && tg.UserId == request.UserId
                                                     && tg.NormalizedName == t)));
            }
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var cards = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.CreatedAt,
                c.Type,
                Tags = _db.CardTags
                    .Where(ct => ct.CardId == c.Id)
                    .Join(_db.Tags, ct => ct.TagId, t => t.Id, (_, t) => t.NormalizedName)
                    .OrderBy(n => n)
                    .ToList(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = cards
            .Select(c => new CardSummaryDto(c.Id, c.Title, c.Tags, c.CreatedAt, c.Type))
            .ToList();

        return Result<PagedResult<CardSummaryDto>>.Success(
            new PagedResult<CardSummaryDto>(items, page, pageSize, total));
    }
}
