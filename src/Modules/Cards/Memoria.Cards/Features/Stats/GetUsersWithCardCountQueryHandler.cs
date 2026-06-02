using MediatR;

using Memoria.Cards.Contracts.Queries;
using Memoria.Cards.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Cards.Features.Stats;

internal sealed class GetUsersWithCardCountQueryHandler
    : IRequestHandler<GetUsersWithCardCountQuery, Result<int>>
{
    private readonly CardsDbContext _db;

    public GetUsersWithCardCountQueryHandler(CardsDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<int>> Handle(GetUsersWithCardCountQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Soft-deleted cards excluded via default query filter — that's what
        // the funnel wants ("user has a live card right now").
        var count = await _db.Cards
            .Select(c => c.UserId)
            .Distinct()
            .CountAsync(ct)
            .ConfigureAwait(false);

        return Result<int>.Success(count);
    }
}
