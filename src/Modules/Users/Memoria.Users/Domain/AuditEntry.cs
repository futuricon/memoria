namespace Memoria.Users.Domain;

/// <summary>
/// Append-only audit row. Every admin read or mutation lands here so we can
/// trace who looked at what. Retention is 365 days, enforced by the
/// <c>AuditLogPruneJob</c> Hangfire recurring job.
/// </summary>
internal sealed class AuditEntry
{
    private AuditEntry()
    {
    }

    public AuditEntry(
        Guid actorUserId,
        string action,
        string subject,
        string? metadataJson,
        DateTime occurredAt)
    {
        Id = Guid.NewGuid();
        ActorUserId = actorUserId;
        Action = action;
        Subject = subject;
        MetadataJson = metadataJson;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string? MetadataJson { get; private set; }
    public DateTime OccurredAt { get; private set; }
}
