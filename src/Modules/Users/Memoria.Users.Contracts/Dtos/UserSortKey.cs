namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// Sort dimensions for the admin users list. Order is admin-facing — keeping
/// this in Contracts so the API/SPA can negotiate via query-string.
/// </summary>
public enum UserSortKey
{
    CreatedAtDesc = 0,
    CreatedAtAsc = 1,
    LastSeenAtDesc = 2,
    DisplayNameAsc = 3,
}
