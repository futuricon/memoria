namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// Lean row for the admin users list. Token totals + cost are joined at the
/// API layer from <c>Ai.GetUsersTokenTotalsQuery</c> — keep this DTO free of
/// any user content so the list endpoint can never leak it.
/// <para>
/// <see cref="Identities"/> lists the auth providers the user has linked
/// (Telegram / Google / GitHub / Email), in linked-at order.
/// </para>
/// </summary>
public sealed record AdminUserSummaryDto(
    Guid Id,
    string DisplayName,
    string? Email,
    Role Role,
    DateTime CreatedAt,
    DateTime? LastSeenAt,
    bool IsBlocked,
    DateTime? DeletedAt,
    IReadOnlyList<AdminUserIdentityDto> Identities);
