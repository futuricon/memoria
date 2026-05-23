using MediatR;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Contracts.Commands;

/// <summary>
/// Обновляет поля карточки. Любое из полей <c>Title/Body/Tags</c> может быть
/// <c>null</c> — тогда оно не изменяется.
/// </summary>
public sealed record UpdateCardCommand(
    Guid UserId,
    Guid CardId,
    string? Title,
    string? Body,
    IReadOnlyList<string>? Tags) : IRequest<Result<CardDto>>;
