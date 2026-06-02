using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;
using Memoria.Users.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Users.Features.Stats;

internal sealed class GetRetentionCohortsQueryHandler
    : IRequestHandler<GetRetentionCohortsQuery, Result<RetentionCohortsDto>>
{
    private const int MaxWindowDays = 365;

    private readonly UsersDbContext _db;
    private readonly TimeProvider _clock;

    public GetRetentionCohortsQueryHandler(UsersDbContext db, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    public async Task<Result<RetentionCohortsDto>> Handle(
        GetRetentionCohortsQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var window = Math.Clamp(request.WindowDays, 1, MaxWindowDays);

        // End the cohort window 30 days ago so the D30 metric has had time
        // to mature for every user in it. The trade-off: the dashboard
        // shows yesterday's retention picture, not today's.
        var now = _clock.GetUtcNow().UtcDateTime;
        var windowEnd = now.AddDays(-30);
        var windowStart = windowEnd.AddDays(-window);

        // Project the deltas in-memory — the cohort is bounded by
        // construction (size ≈ window days × signup rate), so we don't
        // need server-side conditional aggregation.
        var cohort = await _db.Users
            .Where(u => u.CreatedAt >= windowStart && u.CreatedAt < windowEnd)
            .Select(u => new { u.CreatedAt, u.LastSeenAt })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        int d1 = 0, d7 = 0, d30 = 0;
        foreach (var u in cohort)
        {
            if (u.LastSeenAt is null) continue;
            var delta = u.LastSeenAt.Value - u.CreatedAt;
            if (delta >= TimeSpan.FromDays(1)) d1++;
            if (delta >= TimeSpan.FromDays(7)) d7++;
            if (delta >= TimeSpan.FromDays(30)) d30++;
        }

        return Result<RetentionCohortsDto>.Success(new RetentionCohortsDto(
            WindowStart: windowStart,
            WindowEnd: windowEnd,
            Signups: cohort.Count,
            D1Retained: d1,
            D7Retained: d7,
            D30Retained: d30));
    }
}
