namespace Memoria.Users.Domain;

internal sealed class UserIdentity
{
    private UserIdentity()
    {
    }

    public UserIdentity(
        Guid userId,
        IdentityProvider provider,
        string externalId,
        DateTime linkedAt,
        string? externalDisplayName = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Provider = provider;
        ExternalId = externalId;
        LinkedAt = linkedAt;
        ExternalDisplayName = string.IsNullOrWhiteSpace(externalDisplayName)
            ? null
            : externalDisplayName.Trim();
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public IdentityProvider Provider { get; private set; }
    public string ExternalId { get; private set; } = string.Empty;
    public DateTime LinkedAt { get; private set; }

    /// <summary>
    /// Human-friendly handle reported by the provider (e.g. Telegram
    /// <c>@username</c>). Optional — Telegram users may not have one, and
    /// other providers may not expose one. Refreshed on every successful
    /// auth so upstream changes propagate.
    /// </summary>
    public string? ExternalDisplayName { get; private set; }

    /// <summary>
    /// Updates the cached handle to whatever the provider reported on the
    /// current login. Clears the field if the provider stopped reporting one.
    /// </summary>
    public void UpdateExternalDisplayName(string? handle)
    {
        var normalized = string.IsNullOrWhiteSpace(handle) ? null : handle.Trim();
        if (!string.Equals(ExternalDisplayName, normalized, StringComparison.Ordinal))
        {
            ExternalDisplayName = normalized;
        }
    }

    /// <summary>
    /// Repoints this identity at a different user. Used only by the
    /// account-merge flow when transferring identities that the target user
    /// does not already own.
    /// </summary>
    internal void ReassignTo(Guid newUserId) => UserId = newUserId;
}
