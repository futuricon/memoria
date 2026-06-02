using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Contracts.Commands;

/// <summary>
/// Принимает уже-валидированный Telegram Login Widget payload (HMAC проверяет
/// API-слой) и возвращает JWT-пару. Если пользователя с такой Telegram-идентичностью
/// нет — создаётся новый <c>User</c> + <c>UserIdentity</c> (auto-registration).
/// </summary>
public sealed record AuthenticateTelegramWidgetCommand(
    string TelegramId,
    string DisplayName,
    string? Username = null) : IRequest<Result<JwtTokenPairDto>>;
