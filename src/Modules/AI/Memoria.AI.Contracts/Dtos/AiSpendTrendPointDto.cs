namespace Memoria.AI.Contracts.Dtos;

/// <summary>
/// One bucket in the spend-trend chart. <paramref name="DateUtc"/> is the
/// start-of-day UTC for that bucket. Provider is derived from the model
/// name prefix (claude-* vs deepseek-*) so the dashboard can stack the two.
/// </summary>
public sealed record AiSpendTrendPointDto(
    DateTime DateUtc,
    string Provider,
    AiOperation Operation,
    long InputTokens,
    long OutputTokens,
    decimal EstimatedCostUsd,
    int CallCount);
