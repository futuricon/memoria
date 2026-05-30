namespace Memoria.Reviews.Contracts.Dtos;

/// <summary>
/// Aggregated review statistics for a single card. Both averages are normalized
/// to the 0–100 scale so callers can compare or rank Note and Question cards
/// uniformly. <see cref="AvgRating"/> and <see cref="AvgAiScore"/> are
/// <c>null</c> when no review of that flavour exists for the card.
/// </summary>
public sealed record CardGradeStatsDto(
    Guid CardId,
    int ReviewCount,
    double? AvgRating,
    double? AvgAiScore);
