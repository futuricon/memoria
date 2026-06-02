using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Queries;
using Memoria.Reviews.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reviews.Features.Stats;

internal sealed class GetAiCalibrationQueryHandler
    : IRequestHandler<GetAiCalibrationQuery, Result<IReadOnlyList<AiCalibrationBucketDto>>>
{
    private const int MaxDaysBack = 365;

    // Five 20-point buckets covering 0-100 — wide enough to be statistically
    // meaningful at low review volume, narrow enough to show the calibration
    // trend.
    private static readonly (int Lower, int Upper)[] Buckets =
    [
        (0, 20),
        (20, 40),
        (40, 60),
        (60, 80),
        (80, 101),
    ];

    private readonly ReviewsDbContext _db;
    private readonly TimeProvider _clock;

    public GetAiCalibrationQueryHandler(ReviewsDbContext db, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<AiCalibrationBucketDto>>> Handle(
        GetAiCalibrationQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var daysBack = Math.Clamp(request.DaysBack, 1, MaxDaysBack);
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-daysBack);

        var rows = await _db.Reviews
            .Where(r => r.AiScore != null && r.ReviewedAt >= cutoff)
            .Select(r => new { Score = r.AiScore!.Value, r.Rating })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var output = new List<AiCalibrationBucketDto>(Buckets.Length);
        foreach (var (lower, upper) in Buckets)
        {
            int forgot = 0, hard = 0, good = 0, easy = 0;
            foreach (var r in rows)
            {
                if (r.Score < lower || r.Score >= upper) continue;
                switch (r.Rating)
                {
                    case Rating.Forgot: forgot++; break;
                    case Rating.Hard: hard++; break;
                    case Rating.Good: good++; break;
                    case Rating.Easy: easy++; break;
                }
            }

            output.Add(new AiCalibrationBucketDto(
                LowerInclusive: lower,
                UpperExclusive: upper,
                Forgot: forgot,
                Hard: hard,
                Good: good,
                Easy: easy));
        }

        return Result<IReadOnlyList<AiCalibrationBucketDto>>.Success(output);
    }
}
