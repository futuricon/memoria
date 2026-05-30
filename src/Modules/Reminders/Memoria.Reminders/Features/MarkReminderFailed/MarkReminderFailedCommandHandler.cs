using MediatR;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Persistence;
using Memoria.Reminders.Services;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Reminders.Features.MarkReminderFailed;

internal sealed class MarkReminderFailedCommandHandler
    : IRequestHandler<MarkReminderFailedCommand, Result<Unit>>
{
    private readonly RemindersDbContext _db;
    private readonly TimeProvider _clock;
    private readonly DueRemindersDispatcher _dispatcher;
    private readonly ILogger<MarkReminderFailedCommandHandler> _logger;

    public MarkReminderFailedCommandHandler(
        RemindersDbContext db,
        TimeProvider clock,
        DueRemindersDispatcher dispatcher,
        ILogger<MarkReminderFailedCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _clock = clock;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task<Result<Unit>> Handle(MarkReminderFailedCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reminder = await _db.Reminders
            .FirstOrDefaultAsync(r => r.Id == request.ReminderId, ct)
            .ConfigureAwait(false);

        if (reminder is null)
        {
            _logger.LogWarning(
                "MarkReminderFailed: reminder {ReminderId} not found (reason was: {Reason})",
                request.ReminderId, request.Reason);
            return Result<Unit>.Failure(Error.NotFound(
                "reminders.not_found", "Reminder not found."));
        }

        _logger.LogError(
            "MarkReminderFailed: reminder {ReminderId} delivery failed: {Reason}",
            request.ReminderId, request.Reason);

        var now = _clock.GetUtcNow().UtcDateTime;
        try
        {
            reminder.MarkFailed(now);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "MarkReminderFailed: reminder {ReminderId} cannot transition to Failed from current status",
                request.ReminderId);
            return Result<Unit>.Failure(Error.Conflict(
                "reminders.invalid_transition", ex.Message));
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await _dispatcher.EnqueueNextDueAsync(reminder.UserId, now, ct).ConfigureAwait(false);
        return Result<Unit>.Success(Unit.Value);
    }
}
