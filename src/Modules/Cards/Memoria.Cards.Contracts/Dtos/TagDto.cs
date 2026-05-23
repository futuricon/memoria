namespace Memoria.Cards.Contracts.Dtos;

/// <summary>
/// Тег пользователя.
/// </summary>
/// <param name="Id">Идентификатор тега.</param>
/// <param name="Name">Нормализованное имя тега (lowercase, дефисы вместо пробелов).</param>
/// <param name="CardCount">Сколько активных карточек привязано к этому тегу.</param>
public sealed record TagDto(Guid Id, string Name, int CardCount);
