using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Queries;
using Memoria.Reviews.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reviews.Features.CardGradeStats;

/// <summary>
/// Aggregates per-card review stats for a batch of <c>CardIds</c>. Rating enum
/// values map to a 0–100 scale via a SQL CASE so both averages live on the same
/// shelf and can be ranked together (Forgot=0, Hard=33, Good=66, Easy=100).
/// </summary>
internal sealed class GetCardGradeStatsQueryHandler
    : IRequestHandler<GetCardGradeStatsQuery, Result<IReadOnlyList<CardGradeStatsDto>>>
{
    private readonly ReviewsDbContext _db;

    public GetCardGradeStatsQueryHandler(ReviewsDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<IReadOnlyList<CardGradeStatsDto>>> Handle(
        GetCardGradeStatsQuery request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.CardIds.Count == 0)
        {
            return Result<IReadOnlyList<CardGradeStatsDto>>.Success(Array.Empty<CardGradeStatsDto>());
        }

        var ids = request.CardIds.Distinct().ToArray();

        var stats = await _db.Reviews
            .Where(r => r.UserId == request.UserId && ids.Contains(r.CardId))
            .GroupBy(r => r.CardId)
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

        return Result<IReadOnlyList<CardGradeStatsDto>>.Success(stats);
    }
}
