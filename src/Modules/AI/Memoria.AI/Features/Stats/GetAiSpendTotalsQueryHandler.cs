using MediatR;

using Memoria.AI.Contracts.Dtos;
using Memoria.AI.Contracts.Queries;
using Memoria.AI.Persistence;
using Memoria.AI.Pricing;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.AI.Features.Stats;

internal sealed class GetAiSpendTotalsQueryHandler
    : IRequestHandler<GetAiSpendTotalsQuery, Result<AiSpendTotalsDto>>
{
    private const int MaxDaysBack = 365;

    private readonly AiDbContext _db;
    private readonly AiModelPricing _pricing;
    private readonly TimeProvider _clock;

    public GetAiSpendTotalsQueryHandler(AiDbContext db, AiModelPricing pricing, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(pricing);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _pricing = pricing;
        _clock = clock;
    }

    public async Task<Result<AiSpendTotalsDto>> Handle(
        GetAiSpendTotalsQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var daysBack = Math.Clamp(request.DaysBack, 1, MaxDaysBack);
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-daysBack);

        var rows = await _db.Usage
            .Where(u => u.OccurredAt >= cutoff)
            .GroupBy(u => u.Model)
            .Select(g => new
            {
                Model = g.Key,
                InputTokens = g.Sum(x => (long)x.InputTokens),
                OutputTokens = g.Sum(x => (long)x.OutputTokens),
                CallCount = g.Count(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        long totalInput = 0, totalOutput = 0;
        int totalCalls = 0;
        decimal totalCost = 0m;
        foreach (var r in rows)
        {
            totalInput += r.InputTokens;
            totalOutput += r.OutputTokens;
            totalCalls += r.CallCount;
            totalCost += _pricing.Estimate(r.Model, ClampToInt(r.InputTokens), ClampToInt(r.OutputTokens));
        }

        return Result<AiSpendTotalsDto>.Success(new AiSpendTotalsDto(
            TotalInputTokens: totalInput,
            TotalOutputTokens: totalOutput,
            EstimatedCostUsd: totalCost,
            CallCount: totalCalls));
    }

    private static int ClampToInt(long value) =>
        value > int.MaxValue ? int.MaxValue : (int)value;
}
