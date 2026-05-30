using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Queries;
using Memoria.Reviews.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reviews.Features.CardGradeStats;

/// <summary>
/// Ranks the user's cards by lowest combined grade. Combined =
/// COALESCE(AvgAiScore, AvgRating) so Question cards with AI grades use the AI
/// score directly while Note cards (and Question cards graded manually) fall
/// back to the rating-normalized scale.
/// </summary>
internal sealed class GetWorstCardsQueryHandler
    : IRequestHandler<GetWorstCardsQuery, Result<IReadOnlyList<CardGradeStatsDto>>>
{
    private const int MaxTake = 20;

    private readonly ReviewsDbContext _db;

    public GetWorstCardsQueryHandler(ReviewsDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<IReadOnlyList<CardGradeStatsDto>>> Handle(
        GetWorstCardsQuery request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var take = Math.Clamp(request.Take, 1, MaxTake);
        var minReviews = Math.Max(1, request.MinReviews);

        var stats = await _db.Reviews
            .Where(r => r.UserId == request.UserId)
            .GroupBy(r => r.CardId)
            .Where(g => g.Count() >= minReviews)
            .Select(g => new CardGradeStatsDto(
                g.Key,
                g.Count(),
                g.Average(r =>
                    r.Rating == Rating.Forgot ? 0.0 :
                    r.Rating == Rating.Hard ? 33.0 :
                    r.Rating == Rating.Good ? 66.0 :
                    100.0),
                g.Where(r => r.AutoGraded && r.AiScore != null)
                    .Average(r => (double?)r.AiScore)))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var ranked = stats
            .OrderBy(s => s.AvgAiScore ?? s.AvgRating ?? double.MaxValue)
            .Take(take)
            .ToList();

        return Result<IReadOnlyList<CardGradeStatsDto>>.Success(ranked);
    }
}
