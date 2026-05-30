using Hangfire;

using Memoria.Reminders.Domain;
using Memoria.Reminders.Jobs;
using Memoria.Reminders.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoria.Reminders.Services;

/// <summary>
/// Single-in-flight queue dispatcher. After a reminder transitions OUT of
/// <see cref="ReminderStatus.Sent"/> (Confirmed / Skipped / Failed), the user
/// becomes "free" and the next overdue pending reminder for them can be sent.
/// This method finds it and enqueues <see cref="SendReminderJob"/> for it.
/// No-op when the user is still busy (another reminder is Sent) or has nothing
/// overdue waiting.
/// </summary>
internal sealed class DueRemindersDispatcher
{
    private readonly RemindersDbContext _db;
    private readonly IBackgroundJobClient _hangfire;
    private readonly ILogger<DueRemindersDispatcher> _logger;

    public DueRemindersDispatcher(
        RemindersDbContext db,
        IBackgroundJobClient hangfire,
        ILogger<DueRemindersDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(hangfire);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _hangfire = hangfire;
        _logger = logger;
    }

    public async Task EnqueueNextDueAsync(Guid userId, DateTime nowUtc, CancellationToken ct)
    {
        // Defensive: even though callers invoke us right after transitioning
        // a reminder out of Sent, a concurrent path could already have a fresh
        // Sent for this user — don't drop a second one on top.
        var hasInFlight = await _db.Reminders
            .AnyAsync(r => r.UserId == userId && r.Status == ReminderStatus.Sent, ct)
            .ConfigureAwait(false);
        if (hasInFlight)
        {
            return;
        }

        var nextId = await _db.Reminders
            .Where(r => r.UserId == userId
                        && r.Status == ReminderStatus.Pending
                        && r.ScheduledAt <= nowUtc)
            .OrderBy(r => r.ScheduledAt)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (nextId == Guid.Empty)
        {
            return;
        }

        _hangfire.Enqueue<SendReminderJob>(job => job.ExecuteAsync(nextId, CancellationToken.None));
        _logger.LogInformation(
            "Enqueued next due reminder {ReminderId} for user {UserId} after in-flight reminder resolved",
            nextId, userId);
    }
}
