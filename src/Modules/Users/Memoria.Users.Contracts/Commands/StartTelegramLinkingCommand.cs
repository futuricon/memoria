using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Contracts.Commands;

/// <summary>
/// Сценарий 1: пользователь в SPA хочет привязать Telegram.
/// Генерирует одноразовый GUID-токен и deep-link для перехода в бота.
/// </summary>
/// <param name="UserId">ID пользователя, инициирующего привязку.</param>
public sealed record StartTelegramLinkingCommand(Guid UserId) : IRequest<Result<TelegramLinkingTokenDto>>;
