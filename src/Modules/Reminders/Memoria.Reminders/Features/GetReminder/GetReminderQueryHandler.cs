using MediatR;

using Memoria.Reminders.Contracts.Dtos;
using Memoria.Reminders.Contracts.Queries;
using Memoria.Reminders.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reminders.Features.GetReminder;

internal sealed class GetReminderQueryHandler
    : IRequestHandler<GetReminderQuery, Result<ReminderDto>>
{
    private readonly RemindersDbContext _db;

    public GetReminderQueryHandler(RemindersDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<ReminderDto>> Handle(GetReminderQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reminder = await _db.Reminders
            .Where(r => r.Id == request.ReminderId)
            .Select(r => new { r.Id, r.CardId, r.UserId, r.StageNumber, r.Status })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (reminder is null)
        {
            return Result<ReminderDto>.Failure(Error.NotFound(
                "reminders.not_found", "Reminder not found."));
        }

        if (reminder.UserId != request.UserId)
        {
            return Result<ReminderDto>.Failure(Error.Forbidden(
                "reminders.not_owner", "Reminder belongs to another user."));
        }

        return Result<ReminderDto>.Success(new ReminderDto(
            reminder.Id, reminder.CardId, reminder.UserId, reminder.StageNumber, reminder.Status.ToString()));
    }
}
