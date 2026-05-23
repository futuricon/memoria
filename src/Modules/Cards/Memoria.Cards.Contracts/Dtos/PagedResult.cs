namespace Memoria.Cards.Contracts.Dtos;

/// <summary>
/// Универсальная страница результатов запроса.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
