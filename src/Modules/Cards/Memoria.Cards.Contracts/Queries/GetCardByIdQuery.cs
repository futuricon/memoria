using MediatR;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Contracts.Queries;

/// <summary>
/// Возвращает одну карточку с тегами. По умолчанию ищет только среди
/// активных (не-soft-deleted) карточек.
/// </summary>
public sealed record GetCardByIdQuery(
    Guid UserId,
    Guid CardId,
    bool IncludeDeleted = false) : IRequest<Result<CardDto>>;
