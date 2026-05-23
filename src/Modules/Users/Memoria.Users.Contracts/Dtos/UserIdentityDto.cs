namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// Идентичность пользователя у внешнего провайдера (Telegram, Google, GitHub, Email).
/// </summary>
/// <param name="Provider">Имя провайдера (строка).</param>
/// <param name="ExternalId">Идентификатор пользователя у провайдера.</param>
/// <param name="LinkedAt">Когда была привязана идентичность (UTC).</param>
public sealed record UserIdentityDto(
    string Provider,
    string ExternalId,
    DateTime LinkedAt);
