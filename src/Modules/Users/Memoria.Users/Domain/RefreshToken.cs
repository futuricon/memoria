namespace Memoria.Users.Domain;

internal sealed class RefreshToken
{
    private RefreshToken()
    {
    }

    public RefreshToken(Guid userId, string tokenHash, DateTime expiresAt, DateTime createdAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public void Revoke(DateTime utcNow, Guid? replacedBy = null)
    {
        RevokedAt = utcNow;
        ReplacedByTokenId = replacedBy;
    }

    public bool IsActive(DateTime utcNow) => RevokedAt is null && utcNow < ExpiresAt;
}