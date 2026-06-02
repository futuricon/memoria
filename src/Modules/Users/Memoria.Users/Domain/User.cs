using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Domain;

internal sealed class User
{
    private User()
    {
    }

    public User(string displayName, string timeZoneId, DateTime createdAt)
    {
        Id = Guid.NewGuid();
        DisplayName = displayName;
        TimeZoneId = timeZoneId;
        CreatedAt = createdAt;
        Role = Role.User;
        IsBlocked = false;
    }

    public Guid Id { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string TimeZoneId { get; private set; } = "UTC";
    public TimeOnly? QuietHoursStart { get; private set; }
    public TimeOnly? QuietHoursEnd { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    /// <summary>Coarse authorisation tier — bumped from the
    /// <c>Admin:Emails</c> config list at login.</summary>
    public Role Role { get; private set; } = Role.User;

    /// <summary>Last time the user successfully exchanged or refreshed a
    /// token. Used to derive activity status in admin views without
    /// persisting a "status" column.</summary>
    public DateTime? LastSeenAt { get; private set; }

    /// <summary>Explicit suspension by an admin. Distinct from
    /// <see cref="DeletedAt"/> which marks account removal/merge.</summary>
    public bool IsBlocked { get; private set; }

    public void SetEmail(string email) => Email = email;

    public void UpdatePreferences(string timeZoneId, TimeOnly? quietHoursStart, TimeOnly? quietHoursEnd)
    {
        TimeZoneId = timeZoneId;
        QuietHoursStart = quietHoursStart;
        QuietHoursEnd = quietHoursEnd;
    }

    /// <summary>
    /// Clears the email so the unique index (filtered by IS NOT NULL) frees
    /// the slot. Used only by the account-merge flow before soft-deleting
    /// the source user.
    /// </summary>
    internal void ClearEmail() => Email = null;

    /// <summary>
    /// Marks the user as removed. The query filter
    /// (<c>DeletedAt == null</c>) then hides the row from regular reads.
    /// Used only by the account-merge flow.
    /// </summary>
    internal void SoftDelete(DateTime utcNow) => DeletedAt = utcNow;

    /// <summary>Idempotent: only updates when the user isn't already an admin.</summary>
    public void PromoteToAdmin()
    {
        if (Role != Role.Admin)
        {
            Role = Role.Admin;
        }
    }

    /// <summary>Set at every token-issuance handler. Drives DAU/WAU/MAU and
    /// the activity badge in admin views.</summary>
    public void BumpLastSeenAt(DateTime utcNow) => LastSeenAt = utcNow;

    public void Block() => IsBlocked = true;

    public void Unblock() => IsBlocked = false;

    /// <summary>
    /// Called from every token-issuance handler right before
    /// <see cref="Memoria.Users.Services.JwtTokenIssuer.Issue"/>. Bumps
    /// <see cref="LastSeenAt"/> unconditionally and promotes the user to
    /// <see cref="Role.Admin"/> when their email is in the configured admin
    /// allow-list (case-insensitive).
    /// </summary>
    public void MarkTokenIssued(DateTime utcNow, IEnumerable<string> adminEmails)
    {
        ArgumentNullException.ThrowIfNull(adminEmails);

        BumpLastSeenAt(utcNow);

        if (Role == Role.Admin || string.IsNullOrWhiteSpace(Email))
        {
            return;
        }

        foreach (var admin in adminEmails)
        {
            if (string.Equals(admin, Email, StringComparison.OrdinalIgnoreCase))
            {
                PromoteToAdmin();
                return;
            }
        }
    }
}
