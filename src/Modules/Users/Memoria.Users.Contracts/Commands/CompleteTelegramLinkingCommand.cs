using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Contracts.Commands;

/// <summary>
/// Вызывается из <c>Memoria.Bot</c>, когда пользователь кликнул deep-link и
/// бот получил payload <c>link_{Token}</c>. Создаёт привязку Telegram-identity
/// к user. Если Telegram-identity уже принадлежит другому аккаунту, запускает
/// <c>MergeAccountsCommand</c> и возвращает <c>Merged=true</c> с переносимыми
/// счётчиками — бот использует их, чтобы сообщить пользователю что данные
/// были смержены.
/// </summary>
/// <param name="Token">Токен из deep-link.</param>
/// <param name="TelegramId">Telegram chat/user id (строкой, как Telegram отдаёт).</param>
public sealed record CompleteTelegramLinkingCommand(string Token, string TelegramId)
    : IRequest<Result<TelegramLinkingResultDto>>;
