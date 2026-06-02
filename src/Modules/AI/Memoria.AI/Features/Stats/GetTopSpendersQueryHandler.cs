using MediatR;

using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Contracts.Queries;
using Memoria.AI.Persistence;
using Memoria.AI.Pricing;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.AI.Features.Stats;

internal sealed class GetTopSpendersQueryHandler
    : IRequestHandler<GetTopSpendersQuery, Result<IReadOnlyList<TopSpenderDto>>>
{
    private const int MaxTop = 100;

    private readonly AiDbContext _db;
    private readonly AiModelPricing _pricing;

    public GetTopSpendersQueryHandler(AiDbContext db, AiModelPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(pricing);
        _db = db;
        _pricing = pricing;
    }

    public async Task<Result<IReadOnlyList<TopSpenderDto>>> Handle(
        GetTopSpendersQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var top = Math.Clamp(request.Top, 1, MaxTop);

        // Per-(user, model) totals from SQL, then price + sort in memory.
        // We can't sort by priced cost inside SQL because pricing isn't in
        // the DB — it's an Options table the handler holds.
        var rows = await _db.Usage
            .GroupBy(u => new { u.UserId, u.Model })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.Model,
                InputTokens = g.Sum(x => (long)x.InputTokens),
                OutputTokens = g.Sum(x => (long)x.OutputTokens),
                CallCount = g.Count(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var perUser = new Dictionary<Guid, TopSpenderDto>();
        foreach (var r in rows)
        {
            var cost = _pricing.Estimate(r.Model, ClampToInt(r.InputTokens), ClampToInt(r.OutputTokens));
            if (perUser.TryGetValue(r.UserId, out var current))
            {
                perUser[r.UserId] = current with
                {
                    TotalInputTokens = current.TotalInputTokens + r.InputTokens,
                    TotalOutputTokens = current.TotalOutputTokens + r.OutputTokens,
                    EstimatedCostUsd = current.EstimatedCostUsd + cost,
                    CallCount = current.CallCount + r.CallCount,
                };
            }
            else
            {
                perUser[r.UserId] = new TopSpenderDto(
                    UserId: r.UserId,
                    TotalInputTokens: r.InputTokens,
                    TotalOutputTokens: r.OutputTokens,
                    EstimatedCostUsd: cost,
                    CallCount: r.CallCount);
            }
        }

        var ordered = perUser.Values
            .OrderByDescending(s => s.EstimatedCostUsd)
            .Take(top)
            .ToList();

        return Result<IReadOnlyList<TopSpenderDto>>.Success(ordered);
    }

    private static int ClampToInt(long value) =>
        value > int.MaxValue ? int.MaxValue : (int)value;
}
