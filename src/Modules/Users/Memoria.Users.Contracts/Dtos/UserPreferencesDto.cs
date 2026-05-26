namespace Memoria.Users.Contracts.Dtos;

public sealed record UserPreferencesDto(
    Guid UserId,
    string TimeZoneId,
    TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd);