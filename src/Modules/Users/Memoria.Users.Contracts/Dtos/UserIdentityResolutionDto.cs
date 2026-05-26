namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// Минимальный профиль, который нужно знать о пользователе, найденном по
/// внешнему identifier-у (например, Telegram chat id): UserId плюс DisplayName
/// и Email для рендеринга в боте/SPA.
/// </summary>
public sealed record UserIdentityResolutionDto(Guid UserId, string DisplayName, string? Email);
