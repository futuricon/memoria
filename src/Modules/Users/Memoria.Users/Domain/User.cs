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
    }

    public Guid Id { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string TimeZoneId { get; private set; } = "UTC";
    public TimeOnly? QuietHoursStart { get; private set; }
    public TimeOnly? QuietHoursEnd { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

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
}