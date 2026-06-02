using MediatR;

using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Contracts.Queries;
using Memoria.AI.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.AI.Features.Stats;

internal sealed class GetAiFailureRateQueryHandler
    : IRequestHandler<GetAiFailureRateQuery, Result<AiFailureRateDto>>
{
    private const int MaxDaysBack = 365;

    private readonly AiDbContext _db;
    private readonly TimeProvider _clock;

    public GetAiFailureRateQueryHandler(AiDbContext db, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    public async Task<Result<AiFailureRateDto>> Handle(
        GetAiFailureRateQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var daysBack = Math.Clamp(request.DaysBack, 1, MaxDaysBack);
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-daysBack);

        var totals = await _db.Usage
            .Where(u => u.OccurredAt >= cutoff)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Failed = g.Count(x => x.IsFailure),
            })
            .SingleOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return Result<AiFailureRateDto>.Success(new AiFailureRateDto(
            TotalCalls: totals?.Total ?? 0,
            FailedCalls: totals?.Failed ?? 0));
    }
}
