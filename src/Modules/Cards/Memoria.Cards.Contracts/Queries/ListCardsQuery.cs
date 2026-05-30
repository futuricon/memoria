using MediatR;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Contracts.Queries;

/// <summary>
/// Список активных карточек пользователя с фильтром по тегам, поиском и пагинацией.
/// Per-card review stats are merged at the API layer via GetCardGradeStatsQuery —
/// the handler itself does not touch Reviews.
/// </summary>
/// <param name="UserId">Владелец.</param>
/// <param name="Search">Подстрока для поиска по Title/Body (case-insensitive). Может быть null.</param>
/// <param name="Tags">Фильтр по тегам — карточка должна иметь ВСЕ указанные теги.</param>
/// <param name="Page">1-based.</param>
/// <param name="PageSize">1..100.</param>
public sealed record ListCardsQuery(
    Guid UserId,
    string? Search,
    IReadOnlyList<string>? Tags,
    int Page = 1,
    int PageSize = 10) : IRequest<Result<PagedResult<CardSummaryDto>>>;
