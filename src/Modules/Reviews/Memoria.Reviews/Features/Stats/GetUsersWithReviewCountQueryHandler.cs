using MediatR;

using Memoria.Reviews.Contracts.Queries;
using Memoria.Reviews.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reviews.Features.Stats;

internal sealed class GetUsersWithReviewCountQueryHandler
    : IRequestHandler<GetUsersWithReviewCountQuery, Result<int>>
{
    private readonly ReviewsDbContext _db;

    public GetUsersWithReviewCountQueryHandler(ReviewsDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<int>> Handle(GetUsersWithReviewCountQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var count = await _db.Reviews
            .Select(r => r.UserId)
            .Distinct()
            .CountAsync(ct)
            .ConfigureAwait(false);

        return Result<int>.Success(count);
    }
}
