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
}