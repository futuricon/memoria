using Hangfire;

using MediatR;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Reminders.Features.CancelRemindersForCard;

internal sealed class CancelRemindersForCardCommandHandler
    : IRequestHandler<CancelRemindersForCardCommand, Result<Unit>>
{
    private readonly RemindersDbContext _db;
    private readonly IBackgroundJobClient _hangfire;
    private readonly TimeProvider _clock;
    private readonly ILogger<CancelRemindersForCardCommandHandler> _logger;

    public CancelRemindersForCardCommandHandler(
        RemindersDbContext db,
        IBackgroundJobClient hangfire,
        TimeProvider clock,
        ILogger<CancelRemindersForCardCommandHandler> logger)
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

    public async Task<Result<Unit>> Handle(CancelRemindersForCardCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reminders = await _db.Reminders
            .Where(r => r.CardId == request.CardId)
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
                    reminder.Cancel(now);
                    break;

                // Sent, Confirmed, Skipped, Failed, Cancelled — history, untouched.
                default:
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
#pragma warning disable CA1031 // Do not catch general exception types — addendum §2: deletion errors must be logged, not propagated.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(
                ex,
                "Failed to delete Hangfire job {HangfireJobId} during reminder cancellation",
                jobId);
        }
    }
}
