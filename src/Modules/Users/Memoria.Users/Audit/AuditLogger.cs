using System.Text.Json;

using Memoria.Users.Contracts.Abstractions;
using Memoria.Users.Domain;
using Memoria.Users.Persistence;

using Microsoft.Extensions.Logging;

namespace Memoria.Users.Audit;

internal sealed class AuditLogger : IAuditLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly UsersDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(UsersDbContext db, TimeProvider clock, ILogger<AuditLogger> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task LogAsync(
        Guid actorUserId,
        string action,
        string subject,
        object? metadata,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentNullException.ThrowIfNull(subject);

        try
        {
            var metadataJson = metadata is null
                ? null
                : JsonSerializer.Serialize(metadata, JsonOptions);

            var entry = new AuditEntry(
                actorUserId: actorUserId,
                action: action,
                subject: subject,
                metadataJson: metadataJson,
                occurredAt: _clock.GetUtcNow().UtcDateTime);

            _db.AuditLog.Add(entry);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Audit-log failure must never break the admin action it documents
            // — but it's worth a loud log line because operationally this is a
            // privacy / accountability gap.
            _logger.LogError(
                ex,
                "Audit log write failed: actor={Actor}, action={Action}, subject={Subject}",
                actorUserId,
                action,
                subject);
        }
    }
}
