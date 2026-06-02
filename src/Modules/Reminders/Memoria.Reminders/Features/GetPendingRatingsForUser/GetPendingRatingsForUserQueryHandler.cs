using MediatR;

using Memoria.Cards.Contracts.Queries;
using Memoria.Reminders.Contracts.Dtos;
using Memoria.Reminders.Contracts.Queries;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reminders.Features.GetPendingRatingsForUser;

internal sealed class GetPendingRatingsForUserQueryHandler
    : IRequestHandler<GetPendingRatingsForUserQuery, Result<IReadOnlyList<DueReminderDto>>>
{
    private const int MaxTake = 50;

    private readonly RemindersDbContext _db;
    private readonly IMediator _mediator;

    public GetPendingRatingsForUserQueryHandler(RemindersDbContext db, IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(mediator);
        _db = db;
        _mediator = mediator;
    }

    public async Task<Result<IReadOnlyList<DueReminderDto>>> Handle(
        GetPendingRatingsForUserQuery request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var take = Math.Clamp(request.Take, 1, MaxTake);

        // Oldest-first so the user clears the longest-stuck cards first
        // ("you read this 3 days ago, still no rating").
        var reminders = await _db.Reminders
            .Where(r => r.UserId == request.UserId && r.Status == ReminderStatus.Sent)
            .OrderBy(r => r.SentAt ?? r.ScheduledAt)
            .Take(take)
            .Select(r => new
            {
                r.Id,
                r.CardId,
                r.StageNumber,
                When = r.SentAt ?? r.ScheduledAt,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var dtos = new List<DueReminderDto>(reminders.Count);
        foreach (var r in reminders)
        {
            var card = await _mediator
                .Send(new GetCardByIdQuery(request.UserId, r.CardId, IncludeDeleted: true), ct)
                .ConfigureAwait(false);
            var title = card.IsSuccess ? card.Value!.Title : "(deleted card)";
            dtos.Add(new DueReminderDto(r.Id, r.CardId, title, r.When, r.StageNumber));
        }

        return Result<IReadOnlyList<DueReminderDto>>.Success(dtos);
    }
}
