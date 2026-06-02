using MediatR;

using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Contracts.Queries;
using Memoria.AI.Persistence;
using Memoria.AI.Pricing;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.AI.Features.Stats;

internal sealed class GetAiSpendTrendQueryHandler
    : IRequestHandler<GetAiSpendTrendQuery, Result<IReadOnlyList<AiSpendTrendPointDto>>>
{
    private const int MaxDaysBack = 365;

    private readonly AiDbContext _db;
    private readonly AiModelPricing _pricing;
    private readonly TimeProvider _clock;

    public GetAiSpendTrendQueryHandler(AiDbContext db, AiModelPricing pricing, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(pricing);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _pricing = pricing;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<AiSpendTrendPointDto>>> Handle(
        GetAiSpendTrendQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var daysBack = Math.Clamp(request.DaysBack, 1, MaxDaysBack);
        var cutoff = _clock.GetUtcNow().UtcDateTime.Date.AddDays(-daysBack);

        // Group by (day, model, operation) in SQL — the API project still has
        // to bucket by provider at the end so pricing stays close to the model.
        var rows = await _db.Usage
            .Where(u => u.OccurredAt >= cutoff)
            .GroupBy(u => new
            {
                Date = u.OccurredAt.Date,
                u.Model,
                u.Operation,
            })
            .Select(g => new
            {
                g.Key.Date,
                g.Key.Model,
                g.Key.Operation,
                InputTokens = g.Sum(x => (long)x.InputTokens),
                OutputTokens = g.Sum(x => (long)x.OutputTokens),
                CallCount = g.Count(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var folded = new Dictionary<(DateTime Date, string Provider, AiOperation Op), AiSpendTrendPointDto>();
        foreach (var r in rows)
        {
            var provider = AiProviderInference.FromModel(r.Model);
            var cost = _pricing.Estimate(r.Model, ClampToInt(r.InputTokens), ClampToInt(r.OutputTokens));
            var key = (r.Date, provider, r.Operation);

            if (folded.TryGetValue(key, out var current))
            {
                folded[key] = current with
                {
                    InputTokens = current.InputTokens + r.InputTokens,
                    OutputTokens = current.OutputTokens + r.OutputTokens,
                    EstimatedCostUsd = current.EstimatedCostUsd + cost,
                    CallCount = current.CallCount + r.CallCount,
                };
            }
            else
            {
                folded[key] = new AiSpendTrendPointDto(
                    DateUtc: r.Date,
                    Provider: provider,
                    Operation: r.Operation,
                    InputTokens: r.InputTokens,
                    OutputTokens: r.OutputTokens,
                    EstimatedCostUsd: cost,
                    CallCount: r.CallCount);
            }
        }

        var ordered = folded.Values
            .OrderBy(p => p.DateUtc)
            .ThenBy(p => p.Provider, StringComparer.Ordinal)
            .ThenBy(p => p.Operation)
            .ToList();

        return Result<IReadOnlyList<AiSpendTrendPointDto>>.Success(ordered);
    }

    private static int ClampToInt(long value) =>
        value > int.MaxValue ? int.MaxValue : (int)value;
}
