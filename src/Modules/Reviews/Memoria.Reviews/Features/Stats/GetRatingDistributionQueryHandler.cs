using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Queries;
using Memoria.Reviews.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reviews.Features.Stats;

internal sealed class GetRatingDistributionQueryHandler
    : IRequestHandler<GetRatingDistributionQuery, Result<RatingDistributionDto>>
{
    private const int MaxDaysBack = 365;

    private readonly ReviewsDbContext _db;
    private readonly TimeProvider _clock;

    public GetRatingDistributionQueryHandler(ReviewsDbContext db, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    public async Task<Result<RatingDistributionDto>> Handle(
        GetRatingDistributionQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var daysBack = Math.Clamp(request.DaysBack, 1, MaxDaysBack);
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-daysBack);

        var counts = await _db.Reviews
            .Where(r => r.UserId == request.UserId && r.ReviewedAt >= cutoff)
            .GroupBy(r => r.Rating)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        int Pick(Rating rating) =>
            counts.FirstOrDefault(c => c.Key == rating)?.Count ?? 0;

        return Result<RatingDistributionDto>.Success(new RatingDistributionDto(
            Forgot: Pick(Rating.Forgot),
            Hard: Pick(Rating.Hard),
            Good: Pick(Rating.Good),
            Easy: Pick(Rating.Easy)));
    }
}
