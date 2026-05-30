using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Queries;
using Memoria.Reviews.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reviews.Features.Stats;

internal sealed class GetActivityHeatmapQueryHandler
    : IRequestHandler<GetActivityHeatmapQuery, Result<IReadOnlyList<HeatmapDayDto>>>
{
    private const int MaxDaysBack = 365;

    private readonly ReviewsDbContext _db;
    private readonly TimeProvider _clock;

    public GetActivityHeatmapQueryHandler(ReviewsDbContext db, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<HeatmapDayDto>>> Handle(
        GetActivityHeatmapQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var daysBack = Math.Clamp(request.DaysBack, 1, MaxDaysBack);
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-daysBack);

        // Pull timestamps and bucket in memory. Bucketing on the SQL side
        // would need provider-specific date_trunc / cast; the in-memory walk
        // costs nothing for a single user's quarter of reviews.
        var timestamps = await _db.Reviews
            .Where(r => r.UserId == request.UserId && r.ReviewedAt >= cutoff)
            .Select(r => r.ReviewedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var grouped = timestamps
            .GroupBy(DateOnly.FromDateTime)
            .Select(g => new HeatmapDayDto(g.Key, g.Count()))
            .OrderBy(d => d.DateUtc)
            .ToList();

        return Result<IReadOnlyList<HeatmapDayDto>>.Success(grouped);
    }
}
