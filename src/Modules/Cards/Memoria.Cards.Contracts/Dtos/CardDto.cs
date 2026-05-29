namespace Memoria.Cards.Contracts.Dtos;

/// <summary>
/// Полное представление карточки.
/// </summary>
public sealed record CardDto(
    Guid Id,
    string Title,
    string Body,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    CardType Type);
