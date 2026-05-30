namespace Memoria.Cards.Domain;

internal sealed class Tag
{
    private Tag()
    {
    }

    public Tag(Guid userId, string normalizedName, DateTime createdAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        NormalizedName = normalizedName;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string NormalizedName { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Moves ownership to a different user. Used only by the account-merge
    /// flow when no name collision exists on the target side.
    /// </summary>
    internal void ReassignTo(Guid newUserId) => UserId = newUserId;
}
