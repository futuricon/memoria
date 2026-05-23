namespace Memoria.Cards.Contracts.Dtos;

/// <summary>
/// Soft-deleted карточка из «корзины». Содержит дату удаления и счётчик повторений.
/// </summary>
public sealed record TrashedCardDto(
    Guid Id,
    string Title,
    IReadOnlyList<string> Tags,
    DateTime DeletedAt,
    int ReviewsCount);
