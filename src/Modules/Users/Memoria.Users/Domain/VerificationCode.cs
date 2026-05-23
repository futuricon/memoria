namespace Memoria.Users.Domain;

internal sealed class VerificationCode
{
    private VerificationCode()
    {
    }

    public VerificationCode(
        Guid? userId,
        VerificationPurpose purpose,
        string targetIdentifier,
        string codeHash,
        DateTime expiresAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Purpose = purpose;
        TargetIdentifier = targetIdentifier;
        CodeHash = codeHash;
        ExpiresAt = expiresAt;
        AttemptCount = 0;
    }

    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public VerificationPurpose Purpose { get; private set; }
    public string TargetIdentifier { get; private set; } = string.Empty;
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }
    public int AttemptCount { get; private set; }

    public void RegisterAttempt() => AttemptCount++;

    public void MarkConsumed(DateTime utcNow) => ConsumedAt = utcNow;

    public bool IsActive(DateTime utcNow) => ConsumedAt is null && utcNow < ExpiresAt;
}