namespace Memoria.AI.Contracts.Dtos;

/// <summary>
/// Per-user roll-up over <c>ai.ai_usage</c>. Costs are pre-computed against
/// the current <c>Ai:Pricing</c> table so the API doesn't have to ship rates
/// to the frontend.
/// </summary>
public sealed record AiUsageTotalsDto(
    long TotalInputTokens,
    long TotalOutputTokens,
    decimal EstimatedCostUsd,
    DateTime? LastCallAt,
    int CallCount);
