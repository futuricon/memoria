using MediatR;

using Memoria.Cards.Contracts.Queries;
using Memoria.Reminders.Contracts.Dtos;
using Memoria.Reminders.Contracts.Queries;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Persistence;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Queries;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reminders.Features.GetDueRemindersForUser;

internal sealed class GetDueRemindersForUserQueryHandler
    : IRequestHandler<GetDueRemindersForUserQuery, Result<IReadOnlyList<DueReminderDto>>>
{
    private readonly RemindersDbContext _db;
    private readonly IMediator _mediator;

    public GetDueRemindersForUserQueryHandler(RemindersDbContext db, IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(mediator);
        _db = db;
        _mediator = mediator;
    }

    public async Task<Result<IReadOnlyList<DueReminderDto>>> Handle(
        GetDueRemindersForUserQuery request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var prefs = await _mediator
            .Send(new GetUserPreferencesQuery(request.UserId), ct)
            .ConfigureAwait(false);

        TimeZoneInfo tz;
        if (prefs.IsSuccess)
        {
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(prefs.Value!.TimeZoneId);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                tz = TimeZoneInfo.Utc;
            }
        }
        else
        {
            tz = TimeZoneInfo.Utc;
        }

        var dayStartLocal = request.Date.ToDateTime(TimeOnly.MinValue);
        var dayEndLocal = request.Date.ToDateTime(TimeOnly.MaxValue);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(dayStartLocal, DateTimeKind.Unspecified), tz);
        var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(
            DateTime.SpecifyKind(dayEndLocal, DateTimeKind.Unspecified), tz);

        var reminders = await _db.Reminders
            .Where(r => r.UserId == request.UserId
                        && r.Status == ReminderStatus.Pending
                        && r.ScheduledAt >= dayStartUtc
                        && r.ScheduledAt <= dayEndUtc)
            .OrderBy(r => r.ScheduledAt)
            .Select(r => new { r.Id, r.CardId, r.ScheduledAt, r.StageNumber })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var dtos = new List<DueReminderDto>(reminders.Count);
        foreach (var r in reminders)
        {
            var card = await _mediator
                .Send(new GetCardByIdQuery(request.UserId, r.CardId, IncludeDeleted: true), ct)
                .ConfigureAwait(false);
            var title = card.IsSuccess ? card.Value!.Title : "(deleted card)";
            dtos.Add(new DueReminderDto(r.Id, r.CardId, title, r.ScheduledAt, r.StageNumber));
        }

        return Result<IReadOnlyList<DueReminderDto>>.Success(dtos);
    }
}
