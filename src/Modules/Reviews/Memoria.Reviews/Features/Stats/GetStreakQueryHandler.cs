using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Queries;
using Memoria.Reviews.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reviews.Features.Stats;

internal sealed class GetStreakQueryHandler
    : IRequestHandler<GetStreakQuery, Result<StreakDto>>
{
    private readonly ReviewsDbContext _db;
    private readonly TimeProvider _clock;

    public GetStreakQueryHandler(ReviewsDbContext db, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    public async Task<Result<StreakDto>> Handle(GetStreakQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Pull only distinct dates — even a heavy reviewer rarely produces
        // more than a few thousand distinct days, so loading into memory and
        // walking is simpler than the SQL-side window-function alternative.
        var dates = await _db.Reviews
            .Where(r => r.UserId == request.UserId)
            .Select(r => r.ReviewedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (dates.Count == 0)
        {
            return Result<StreakDto>.Success(new StreakDto(Current: 0, Longest: 0, LastReviewedOnUtc: null));
        }

        var distinct = dates
            .Select(DateOnly.FromDateTime)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var yesterday = today.AddDays(-1);

        // Current streak: walk back from today. Allow today to be blank
        // (grace day) so the user doesn't lose a long streak before their
        // first review of the day.
        var current = 0;
        var cursor = today;
        if (!distinct.Contains(today))
        {
            // No review today → either yesterday closes the streak, or it's broken.
            if (distinct.Contains(yesterday))
            {
                cursor = yesterday;
            }
            else
            {
                cursor = today; // sentinel — loop won't iterate
            }
        }
        while (distinct.Contains(cursor))
        {
            current++;
            cursor = cursor.AddDays(-1);
        }

        // Longest streak: scan ascending, count consecutive runs.
        var ascending = distinct.OrderBy(d => d).ToList();
        var longest = 0;
        var run = 0;
        DateOnly? prev = null;
        foreach (var d in ascending)
        {
            if (prev is not null && d == prev.Value.AddDays(1))
            {
                run++;
            }
            else
            {
                run = 1;
            }
            if (run > longest) longest = run;
            prev = d;
        }

        return Result<StreakDto>.Success(new StreakDto(
            Current: current,
            Longest: longest,
            LastReviewedOnUtc: distinct[0]));
    }
}
