using MediatR;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Reminders.Features.ConfirmReminder;

internal sealed class ConfirmReminderCommandHandler
    : IRequestHandler<ConfirmReminderCommand, Result<Unit>>
{
    private readonly RemindersDbContext _db;
    private readonly TimeProvider _clock;

    public ConfirmReminderCommandHandler(RemindersDbContext db, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(ConfirmReminderCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reminder = await _db.Reminders
            .FirstOrDefaultAsync(r => r.Id == request.ReminderId, ct)
            .ConfigureAwait(false);

        if (reminder is null)
        {
            return Result<Unit>.Failure(Error.NotFound(
                "reminders.not_found", "Reminder not found."));
        }

        if (reminder.UserId != request.UserId)
        {
            return Result<Unit>.Failure(Error.Forbidden(
                "reminders.not_owner", "Reminder belongs to another user."));
        }

        try
        {
            reminder.Confirm(_clock.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException ex)
        {
            return Result<Unit>.Failure(Error.Conflict(
                "reminders.invalid_transition", ex.Message));
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result<Unit>.Success(Unit.Value);
    }
}
