using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Contracts.Queries;

/// <summary>
/// Резолвит пользователя по Telegram external id. Используется бот-модулем
/// для определения "кто сейчас пишет" в каждом callback'е/команде.
/// </summary>
public sealed record GetUserByTelegramIdQuery(string TelegramId)
    : IRequest<Result<UserIdentityResolutionDto>>;
