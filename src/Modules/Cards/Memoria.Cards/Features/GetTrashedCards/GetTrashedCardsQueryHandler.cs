using MediatR;
using Microsoft.EntityFrameworkCore;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Queries;
using Memoria.Cards.Persistence;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Features.GetTrashedCards;

internal sealed class GetTrashedCardsQueryHandler : IRequestHandler<GetTrashedCardsQuery, Result<PagedResult<TrashedCardDto>>>
{
    private const int MaxPageSize = 100;
    private readonly CardsDbContext _db;

    public GetTrashedCardsQueryHandler(CardsDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<PagedResult<TrashedCardDto>>> Handle(
        GetTrashedCardsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

        var query = _db.Cards
            .IgnoreQueryFilters()
            .Where(c => c.UserId == request.UserId && c.DeletedAt != null);

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var cards = await query
            .OrderByDescending(c => c.DeletedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Title,
                DeletedAt = c.DeletedAt!.Value,
                Tags = _db.CardTags
                    .Where(ct => ct.CardId == c.Id)
                    .Join(_db.Tags, ct => ct.TagId, t => t.Id, (_, t) => t.NormalizedName)
                    .OrderBy(n => n)
                    .ToList(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // ReviewsCount = 0 пока (Reviews-модуль наполнится на Stage 8).
        var items = cards
            .Select(c => new TrashedCardDto(c.Id, c.Title, c.Tags, c.DeletedAt, ReviewsCount: 0))
            .ToList();

        return Result<PagedResult<TrashedCardDto>>.Success(
            new PagedResult<TrashedCardDto>(items, page, pageSize, total));
    }
}
