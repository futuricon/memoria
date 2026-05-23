namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// Одноразовый токен для привязки Telegram через deep-link.
/// </summary>
/// <param name="Token">Сырой токен, передаётся в payload <c>/start link_{Token}</c>.</param>
/// <param name="DeepLink">Полный готовый URL вида <c>https://t.me/{BotUsername}?start=link_{Token}</c>.</param>
/// <param name="ExpiresAt">UTC-момент истечения токена.</param>
public sealed record TelegramLinkingTokenDto(
    string Token,
    string DeepLink,
    DateTime ExpiresAt);
