using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;
using Memoria.Users.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Users.Features.Stats;

internal sealed class GetActiveUserCountsQueryHandler
    : IRequestHandler<GetActiveUserCountsQuery, Result<ActiveUserCountsDto>>
{
    private readonly UsersDbContext _db;
    private readonly TimeProvider _clock;

    public GetActiveUserCountsQueryHandler(UsersDbContext db, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    public async Task<Result<ActiveUserCountsDto>> Handle(
        GetActiveUserCountsQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.GetUtcNow().UtcDateTime;
        var dayAgo = now.AddDays(-1);
        var weekAgo = now.AddDays(-7);
        var monthAgo = now.AddDays(-30);

        // Soft-deleted users excluded via the default query filter — that's
        // the right denominator: a deleted account isn't "active".
        var dau = await _db.Users
            .CountAsync(u => u.LastSeenAt != null && u.LastSeenAt >= dayAgo, ct)
            .ConfigureAwait(false);
        var wau = await _db.Users
            .CountAsync(u => u.LastSeenAt != null && u.LastSeenAt >= weekAgo, ct)
            .ConfigureAwait(false);
        var mau = await _db.Users
            .CountAsync(u => u.LastSeenAt != null && u.LastSeenAt >= monthAgo, ct)
            .ConfigureAwait(false);

        return Result<ActiveUserCountsDto>.Success(new ActiveUserCountsDto(dau, wau, mau));
    }
}
