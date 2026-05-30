using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Queries;
using Memoria.Reviews.Domain;
using Memoria.Reviews.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reviews.Features.Stats;

/// <summary>
/// Walks the user's reviews newest-first, grouped by card, and emits cards
/// whose first <c>MinConsecutiveForgot</c> reviews are all <c>Forgot</c>.
/// Reads in memory because the "first N" predicate per card is awkward on
/// the SQL side and the per-user history is small.
/// </summary>
internal sealed class GetStuckCardCandidatesQueryHandler
    : IRequestHandler<GetStuckCardCandidatesQuery, Result<IReadOnlyList<StuckCardCandidateDto>>>
{
    private const int MaxTake = 50;

    private readonly ReviewsDbContext _db;

    public GetStuckCardCandidatesQueryHandler(ReviewsDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<IReadOnlyList<StuckCardCandidateDto>>> Handle(
        GetStuckCardCandidatesQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var threshold = Math.Max(1, request.MinConsecutiveForgot);
        var take = Math.Clamp(request.Take, 1, MaxTake);

        var reviews = await _db.Reviews
            .Where(r => r.UserId == request.UserId)
            .OrderByDescending(r => r.ReviewedAt)
            .Select(r => new ReviewProjection(r.CardId, r.Rating, r.ReviewedAt))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var byCard = reviews
            .GroupBy(r => r.CardId)
            .Where(g => g.Count() >= threshold
                        && g.Take(threshold).All(r => r.Rating == Rating.Forgot))
            .Select(g => new StuckCardCandidateDto(
                CardId: g.Key,
                ConsecutiveForgotCount: CountLeadingForgot(g),
                LastReviewedAt: g.Max(r => r.ReviewedAt)))
            .OrderByDescending(d => d.ConsecutiveForgotCount)
            .ThenByDescending(d => d.LastReviewedAt)
            .Take(take)
            .ToList();

        return Result<IReadOnlyList<StuckCardCandidateDto>>.Success(byCard);
    }

    private static int CountLeadingForgot(IEnumerable<ReviewProjection> group)
    {
        var count = 0;
        foreach (var r in group)
        {
            if (r.Rating == Rating.Forgot) count++;
            else break;
        }
        return count;
    }

    private sealed record ReviewProjection(Guid CardId, Rating Rating, DateTime ReviewedAt);
}
