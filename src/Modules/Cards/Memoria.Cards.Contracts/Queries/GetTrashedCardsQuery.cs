using MediatR;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Contracts.Queries;

/// <summary>
/// Возвращает soft-deleted карточки пользователя (отсортированы по <c>DeletedAt DESC</c>).
/// </summary>
public sealed record GetTrashedCardsQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 10) : IRequest<Result<PagedResult<TrashedCardDto>>>;
