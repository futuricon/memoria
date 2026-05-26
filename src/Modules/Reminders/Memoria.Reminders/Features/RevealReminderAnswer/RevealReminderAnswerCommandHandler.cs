using MediatR;

using Memoria.Cards.Contracts.Queries;
using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Contracts.Dtos;
using Memoria.Reminders.Domain;
using Memoria.Reminders.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Reminders.Features.RevealReminderAnswer;

internal sealed class RevealReminderAnswerCommandHandler
    : IRequestHandler<RevealReminderAnswerCommand, Result<RevealedAnswerDto>>
{
    private readonly RemindersDbContext _db;
    private readonly IMediator _mediator;

    public RevealReminderAnswerCommandHandler(RemindersDbContext db, IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(mediator);
        _db = db;
        _mediator = mediator;
    }

    public async Task<Result<RevealedAnswerDto>> Handle(
        RevealReminderAnswerCommand request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reminder = await _db.Reminders
            .FirstOrDefaultAsync(r => r.Id == request.ReminderId, ct)
            .ConfigureAwait(false);

        if (reminder is null)
        {
            return Result<RevealedAnswerDto>.Failure(Error.NotFound(
                "reminders.not_found", "Reminder not found."));
        }

        if (reminder.UserId != request.UserId)
        {
            return Result<RevealedAnswerDto>.Failure(Error.Forbidden(
                "reminders.not_owner", "Reminder belongs to another user."));
        }

        if (reminder.Status is not (ReminderStatus.Sent or ReminderStatus.Confirmed or ReminderStatus.Skipped))
        {
            return Result<RevealedAnswerDto>.Failure(Error.Conflict(
                "reminders.cannot_reveal_yet",
                $"Cannot reveal answer for reminder in status {reminder.Status}."));
        }

        var cardResult = await _mediator
            .Send(new GetCardByIdQuery(request.UserId, reminder.CardId, IncludeDeleted: true), ct)
            .ConfigureAwait(false);

        if (cardResult.IsFailure)
        {
            return Result<RevealedAnswerDto>.Failure(cardResult.Error!);
        }

        var card = cardResult.Value!;
        return new RevealedAnswerDto(card.Id, card.Title, card.Body);
    }
}
