namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// Coarse-grained authorisation tier. Mapped to a JWT role claim and gated
/// at the API edge by the <c>"admin"</c> policy. Bootstrapped from the
/// <c>Admin:Emails</c> config list at every token-issuance handler.
/// </summary>
public enum Role
{
    User = 0,
    Admin = 1,
}
