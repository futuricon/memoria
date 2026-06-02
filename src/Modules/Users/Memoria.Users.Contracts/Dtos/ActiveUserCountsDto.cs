namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// Distinct users seen in trailing 1 / 7 / 30 days, derived from
/// <c>LastSeenAt</c>. Excludes soft-deleted accounts.
/// </summary>
public sealed record ActiveUserCountsDto(
    int Dau,
    int Wau,
    int Mau);
