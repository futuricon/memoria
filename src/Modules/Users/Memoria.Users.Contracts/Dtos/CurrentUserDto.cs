namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// Полный профиль текущего аутентифицированного пользователя для эндпоинта
/// <c>GET /api/v1/users/me</c>.
/// </summary>
public sealed record CurrentUserDto(
    Guid Id,
    string DisplayName,
    string? Email,
    string TimeZoneId,
    TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd,
    DateTime CreatedAt);
