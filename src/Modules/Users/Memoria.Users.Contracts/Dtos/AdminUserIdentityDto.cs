namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// One linked identity row on the admin user detail page. <see cref="Handle"/>
/// is the provider's user-facing name (Telegram <c>@username</c>) — refreshed
/// on every successful auth, may be <c>null</c> if the provider doesn't expose
/// one or the user never set one.
/// </summary>
public sealed record AdminUserIdentityDto(string Provider, string? Handle);
