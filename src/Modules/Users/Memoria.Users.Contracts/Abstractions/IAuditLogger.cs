namespace Memoria.Users.Contracts.Abstractions;

/// <summary>
/// Append a single row to <c>users.audit_log</c>. Called by admin endpoints
/// for every read or mutation. The metadata payload is JSON-serialised and
/// must never contain user content (per the admin-DTO content-free rule).
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(
        Guid actorUserId,
        string action,
        string subject,
        object? metadata,
        CancellationToken ct);
}
