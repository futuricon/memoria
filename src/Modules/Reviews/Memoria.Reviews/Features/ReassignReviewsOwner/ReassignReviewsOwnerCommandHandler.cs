using MediatR;

using Memoria.Reviews.Contracts.Commands;
using Memoria.Reviews.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reviews.Features.ReassignReviewsOwner;

/// <summary>
/// Re-parents every review owned by <c>SourceUserId</c> to <c>TargetUserId</c>.
/// Load-and-update (not <c>ExecuteUpdate</c>) for InMemory-provider parity
/// in unit tests.
/// </summary>
internal sealed class ReassignReviewsOwnerCommandHandler
    : IRequestHandler<ReassignReviewsOwnerCommand, Result<int>>
{
    private readonly ReviewsDbContext _db;

    public ReassignReviewsOwnerCommandHandler(ReviewsDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<int>> Handle(ReassignReviewsOwnerCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SourceUserId == request.TargetUserId)
        {
            return Result<int>.Success(0);
        }

        var reviews = await _db.Reviews
            .Where(r => r.UserId == request.SourceUserId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var review in reviews)
        {
            review.ReassignTo(request.TargetUserId);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result<int>.Success(reviews.Count);
    }
}
