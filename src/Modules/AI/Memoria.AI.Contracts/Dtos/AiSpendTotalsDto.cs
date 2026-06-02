namespace Memoria.AI.Contracts.Dtos;

/// <summary>
/// Aggregate spend across all users in a trailing window. Used by the
/// "cost per active user" KPI, divided at the API edge by the
/// active-user count from the Users module.
/// </summary>
public sealed record AiSpendTotalsDto(
    long TotalInputTokens,
    long TotalOutputTokens,
    decimal EstimatedCostUsd,
    int CallCount);
