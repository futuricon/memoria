using MediatR;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Contracts.Queries;

/// <summary>
/// Список тегов пользователя со счётчиком привязанных активных карточек.
/// </summary>
public sealed record GetTagsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<TagDto>>>;
