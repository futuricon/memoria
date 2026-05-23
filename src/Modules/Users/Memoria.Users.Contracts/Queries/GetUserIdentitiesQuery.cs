using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Contracts.Queries;

/// <summary>
/// Возвращает список привязанных идентичностей пользователя.
/// Используется в SPA для отображения «привязанных аккаунтов» в профиле.
/// </summary>
/// <param name="UserId">ID пользователя.</param>
public sealed record GetUserIdentitiesQuery(Guid UserId) : IRequest<Result<IReadOnlyList<UserIdentityDto>>>;
