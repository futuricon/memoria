using Memoria.Users.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Memoria.Users.Jobs;

/// <summary>
/// Hangfire recurring job that hard-deletes audit rows older than the
/// retention window (365 days). Runs daily; uses ExecuteDeleteAsync so the
/// statement is a single DB round-trip.
/// <para>
/// Resolves <see cref="UsersDbContext"/> via <see cref="IServiceScopeFactory"/>
/// because the DbContext is module-internal — keeping its concrete type out
/// of the public Hangfire-facing constructor signature.
/// </para>
/// </summary>
public sealed class AuditLogPruneJob
{
    public const int RetentionDays = 365;

    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuditLogPruneJob> _logger;

    public AuditLogPruneJob(
        IServiceScopeFactory scopes,
        TimeProvider clock,
        ILogger<AuditLogPruneJob> logger)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct)
    {
        var cutoff = _clock.GetUtcNow().UtcDateTime.AddDays(-RetentionDays);

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        var deleted = await db.AuditLog
            .Where(a => a.OccurredAt < cutoff)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "AuditLogPruneJob: deleted {Count} rows older than {Cutoff:O}",
            deleted, cutoff);
    }
}
