namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// Часовой пояс из системного каталога, в формате, удобном для выбора в UI.
/// </summary>
/// <param name="Id">IANA-идентификатор (на Linux/Mac в .NET 8+ с ICU) либо
/// Windows-идентификатор (если процесс крутится на Windows без ICU).</param>
/// <param name="DisplayName">Локализованное имя зоны с UTC-смещением, например
/// "(UTC+05:00) Tashkent".</param>
public sealed record TimeZoneDto(string Id, string DisplayName);
