using MediatR;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Contracts.Commands;

/// <summary>
/// Создаёт новую карточку, нормализует и привязывает теги, возвращает DTO.
/// </summary>
/// <param name="UserId">Владелец карточки (из auth-контекста).</param>
/// <param name="Title">Тема (1..200 символов).</param>
/// <param name="Body">Тело (1..4000 символов).</param>
/// <param name="Tags">Сырой список тегов; нормализация и дедуп — внутри handler-а.</param>
public sealed record AddCardCommand(
    Guid UserId,
    string Title,
    string Body,
    IReadOnlyList<string> Tags) : IRequest<Result<CardDto>>;
