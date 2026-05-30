namespace Memoria.Cards.Contracts.Dtos;

/// <summary>
/// Сводное представление карточки для списков (без тела). The optional grade
/// stats (<paramref name="ReviewCount"/>, <paramref name="AvgRating"/>,
/// <paramref name="AvgAiScore"/>) are populated by SPA-facing callers via the
/// Reviews aggregation query; the bot ignores them.
/// </summary>
public sealed record CardSummaryDto(
    Guid Id,
    string Title,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt,
    CardType Type,
    int ReviewCount = 0,
    double? AvgRating = null,
    double? AvgAiScore = null,
    bool IsPaused = false,
    int? PausedAtStage = null);
