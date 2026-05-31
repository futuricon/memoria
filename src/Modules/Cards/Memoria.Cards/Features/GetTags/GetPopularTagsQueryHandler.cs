using MediatR;
using Microsoft.EntityFrameworkCore;
using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Queries;
using Memoria.Cards.Persistence;
using Memoria.Shared.Kernel.Results;
using Npgsql;

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
    
        var sql = @"
        SELECT 
            t.id AS Id,
            t.normalized_name AS Name,
            COUNT(ct.card_id) AS CardCount
        FROM cards.tags t
        INNER JOIN cards.card_tags ct ON ct.tag_id = t.id
        INNER JOIN cards.cards c ON c.id = ct.card_id
        WHERE t.user_id = @UserId
            AND c.deleted_at IS NULL
        GROUP BY t.id, t.normalized_name
        HAVING COUNT(ct.card_id) > 0
        ORDER BY CardCount DESC, Name ASC
        LIMIT @Count";

        var items = await _db.Database
            .SqlQueryRaw<TagDto>(sql, 
                new NpgsqlParameter("@UserId", request.UserId),
                new NpgsqlParameter("@Count", count))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<TagDto>>.Success(items);
    }
}