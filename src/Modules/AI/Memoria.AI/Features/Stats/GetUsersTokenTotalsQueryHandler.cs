using MediatR;

using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Contracts.Queries;
using Memoria.AI.Persistence;
using Memoria.AI.Pricing;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.AI.Features.Stats;

internal sealed class GetUsersTokenTotalsQueryHandler
    : IRequestHandler<GetUsersTokenTotalsQuery, Result<IReadOnlyDictionary<Guid, AiUsageTotalsDto>>>
{
    private readonly AiDbContext _db;
    private readonly AiModelPricing _pricing;

    public GetUsersTokenTotalsQueryHandler(AiDbContext db, AiModelPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(pricing);
        _db = db;
        _pricing = pricing;
    }

    public async Task<Result<IReadOnlyDictionary<Guid, AiUsageTotalsDto>>> Handle(
        GetUsersTokenTotalsQuery request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.UserIds.Count == 0)
        {
            return Result<IReadOnlyDictionary<Guid, AiUsageTotalsDto>>.Success(
                new Dictionary<Guid, AiUsageTotalsDto>());
        }

        // Pull per-(user, model) totals from SQL — model-level grouping is
        // what we need to apply pricing without dragging every row out.
        var rows = await _db.Usage
            .Where(u => request.UserIds.Contains(u.UserId))
            .GroupBy(u => new { u.UserId, u.Model })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.Model,
                InputTokens = g.Sum(x => (long)x.InputTokens),
                OutputTokens = g.Sum(x => (long)x.OutputTokens),
                LastCallAt = g.Max(x => x.OccurredAt),
                CallCount = g.Count(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var totals = new Dictionary<Guid, AiUsageTotalsDto>();
        foreach (var r in rows)
        {
            var inputTokensInt = ClampToInt(r.InputTokens);
            var outputTokensInt = ClampToInt(r.OutputTokens);
            var cost = _pricing.Estimate(r.Model, inputTokensInt, outputTokensInt);

            if (totals.TryGetValue(r.UserId, out var current))
            {
                totals[r.UserId] = current with
                {
                    TotalInputTokens = current.TotalInputTokens + r.InputTokens,
                    TotalOutputTokens = current.TotalOutputTokens + r.OutputTokens,
                    EstimatedCostUsd = current.EstimatedCostUsd + cost,
                    LastCallAt = current.LastCallAt is null || r.LastCallAt > current.LastCallAt
                        ? r.LastCallAt
                        : current.LastCallAt,
                    CallCount = current.CallCount + r.CallCount,
                };
            }
            else
            {
                totals[r.UserId] = new AiUsageTotalsDto(
                    TotalInputTokens: r.InputTokens,
                    TotalOutputTokens: r.OutputTokens,
                    EstimatedCostUsd: cost,
                    LastCallAt: r.LastCallAt,
                    CallCount: r.CallCount);
            }
        }

        return Result<IReadOnlyDictionary<Guid, AiUsageTotalsDto>>.Success(totals);
    }

    private static int ClampToInt(long value) =>
        value > int.MaxValue ? int.MaxValue : (int)value;
}
