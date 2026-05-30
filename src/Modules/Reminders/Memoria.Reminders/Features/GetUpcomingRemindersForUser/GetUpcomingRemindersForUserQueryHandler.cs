using MediatR;

using Memoria.Cards.Contracts.Queries;
using Memoria.Reminders.Contracts.Dtos;
using Memoria.Reminders.Contracts.Queries;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reminders.Features.GetUpcomingRemindersForUser;

internal sealed class GetUpcomingRemindersForUserQueryHandler
    : IRequestHandler<GetUpcomingRemindersForUserQuery, Result<IReadOnlyList<DueReminderDto>>>
{
    private const int MaxTake = 50;

    private readonly RemindersDbContext _db;
    private readonly IMediator _mediator;

    public GetUpcomingRemindersForUserQueryHandler(RemindersDbContext db, IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(mediator);
        _db = db;
        _mediator = mediator;
    }

    public async Task<Result<IReadOnlyList<DueReminderDto>>> Handle(
        GetUpcomingRemindersForUserQuery request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var take = Math.Clamp(request.Take, 1, MaxTake);

        var reminders = await _db.Reminders
            .Where(r => r.UserId == request.UserId && r.Status == ReminderStatus.Pending)
            .OrderBy(r => r.ScheduledAt)
            .Take(take)
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
