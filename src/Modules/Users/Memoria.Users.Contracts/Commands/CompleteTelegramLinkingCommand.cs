using MediatR;

using Memoria.Shared.Kernel.Results;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Users.Contracts.Commands;

/// <summary>
/// Вызывается из <c>Memoria.Bot</c>, когда пользователь кликнул deep-link и
/// бот получил payload <c>link_{Token}</c>. Создаёт привязку Telegram-identity к user.
/// </summary>
/// <param name="Token">Токен из deep-link.</param>
/// <param name="TelegramId">Telegram chat/user id (строкой, как Telegram отдаёт).</param>
public sealed record CompleteTelegramLinkingCommand(string Token, string TelegramId) : IRequest<Result<Unit>>;
