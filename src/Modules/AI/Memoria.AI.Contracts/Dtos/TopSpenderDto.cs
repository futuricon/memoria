namespace Memoria.AI.Contracts.Dtos;

/// <summary>
/// One row in the top-N spenders list. UserId only — display name / email
/// are joined at the API layer from Users.
/// </summary>
public sealed record TopSpenderDto(
    Guid UserId,
    long TotalInputTokens,
    long TotalOutputTokens,
    decimal EstimatedCostUsd,
    int CallCount);
