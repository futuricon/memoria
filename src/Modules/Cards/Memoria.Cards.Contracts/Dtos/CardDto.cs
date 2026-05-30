namespace Memoria.Cards.Contracts.Dtos;

/// <summary>
/// Полное представление карточки. The optional grade stats
/// (<paramref name="ReviewCount"/>, <paramref name="AvgRating"/>,
/// <paramref name="AvgAiScore"/>) are populated by SPA-facing callers via the
/// Reviews aggregation query; the bot ignores them.
/// </summary>
public sealed record CardDto(
    Guid Id,
    string Title,
    string Body,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    CardType Type,
    int ReviewCount = 0,
    double? AvgRating = null,
    double? AvgAiScore = null);
