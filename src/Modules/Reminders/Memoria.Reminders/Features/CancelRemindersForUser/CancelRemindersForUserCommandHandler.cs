using Hangfire;

using MediatR;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Reminders.Features.CancelRemindersForUser;

/// <summary>
/// Bulk-cancels every reminder owned by <c>UserId</c>: Pending rows are
/// removed (and their Hangfire jobs deleted), Sending/Sent rows are marked
/// Cancelled (kept for audit). Mirrors the per-card cancel in
/// <see cref="Features.CancelRemindersForCard.CancelRemindersForCardCommandHandler"/>
/// but scoped to the whole user — used by the account-merge flow.
/// </summary>
internal sealed class CancelRemindersForUserCommandHandler
    : IRequestHandler<CancelRemindersForUserCommand, Result<Unit>>
{
    private readonly RemindersDbContext _db;
    private readonly IBackgroundJobClient _hangfire;
    private readonly TimeProvider _clock;
    private readonly ILogger<CancelRemindersForUserCommandHandler> _logger;

    public CancelRemindersForUserCommandHandler(
        RemindersDbContext db,
        IBackgroundJobClient hangfire,
        TimeProvider clock,
        ILogger<CancelRemindersForUserCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(hangfire);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _hangfire = hangfire;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<Unit>> Handle(CancelRemindersForUserCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reminders = await _db.Reminders
            .Where(r => r.UserId == request.UserId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (reminders.Count == 0)
        {
            return Result<Unit>.Success(Unit.Value);
        }

        var now = _clock.GetUtcNow().UtcDateTime;

        foreach (var reminder in reminders)
        {
            switch (reminder.Status)
            {
                case ReminderStatus.Pending:
                    if (reminder.HangfireJobId is not null)
                    {
                        TryDeleteHangfireJob(reminder.HangfireJobId);
                    }
                    _db.Reminders.Remove(reminder);
                    break;

                case ReminderStatus.Sending:
                case ReminderStatus.Sent:
                    reminder.Cancel(now);
                    break;

                default:
                    // Confirmed / Skipped / Failed / Cancelled are terminal history.
                    break;
            }
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result<Unit>.Success(Unit.Value);
    }

    private void TryDeleteHangfireJob(string jobId)
    {
        try
        {
            _hangfire.Delete(jobId);
        }
#pragma warning disable CA1031 // Logging the failure is the appropriate response — Hangfire-side errors must not block the user-merge.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(
                ex,
                "Failed to delete Hangfire job {HangfireJobId} during bulk cancel",
                jobId);
        }
    }
}
