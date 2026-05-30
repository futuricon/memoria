namespace Memoria.Users.Domain;

internal sealed class UserIdentity
{
    private UserIdentity()
    {
    }

    public UserIdentity(Guid userId, IdentityProvider provider, string externalId, DateTime linkedAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Provider = provider;
        ExternalId = externalId;
        LinkedAt = linkedAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public IdentityProvider Provider { get; private set; }
    public string ExternalId { get; private set; } = string.Empty;
    public DateTime LinkedAt { get; private set; }

    /// <summary>
    /// Repoints this identity at a different user. Used only by the
    /// account-merge flow when transferring identities that the target user
    /// does not already own.
    /// </summary>
    internal void ReassignTo(Guid newUserId) => UserId = newUserId;
}