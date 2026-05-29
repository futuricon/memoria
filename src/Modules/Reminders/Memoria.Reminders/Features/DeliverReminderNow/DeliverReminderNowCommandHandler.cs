using Hangfire;

using MediatR;

using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Jobs;
using Memoria.Reminders.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Reminders.Features.DeliverReminderNow;

internal sealed class DeliverReminderNowCommandHandler
    : IRequestHandler<DeliverReminderNowCommand, Result<Unit>>
{
    private readonly RemindersDbContext _db;
    private readonly IBackgroundJobClient _hangfire;

    public DeliverReminderNowCommandHandler(RemindersDbContext db, IBackgroundJobClient hangfire)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(hangfire);
        _db = db;
        _hangfire = hangfire;
    }

    public async Task<Result<Unit>> Handle(DeliverReminderNowCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reminder = await _db.Reminders
            .FirstOrDefaultAsync(r => r.Id == request.ReminderId, ct)
            .ConfigureAwait(false);

        if (reminder is null)
        {
            return Result<Unit>.Failure(Error.NotFound("reminders.not_found", "Reminder not found."));
        }

        if (reminder.UserId != request.UserId)
        {
            return Result<Unit>.Failure(Error.Forbidden(
                "reminders.not_owner", "Reminder belongs to another user."));
        }

        if (reminder.Status != ReminderStatus.Pending)
        {
            return Result<Unit>.Failure(Error.Conflict(
                "reminders.not_pending", "This reminder is not awaiting delivery."));
        }

        var reminderId = reminder.Id;
        _hangfire.Enqueue<SendReminderJob>(job => job.ExecuteAsync(reminderId, CancellationToken.None));

        return Result<Unit>.Success(Unit.Value);
    }
}
