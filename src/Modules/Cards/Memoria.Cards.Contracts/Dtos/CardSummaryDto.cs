namespace Memoria.Cards.Contracts.Dtos;

/// <summary>
/// Сводное представление карточки для списков (без тела).
/// </summary>
public sealed record CardSummaryDto(
    Guid Id,
    string Title,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt);
