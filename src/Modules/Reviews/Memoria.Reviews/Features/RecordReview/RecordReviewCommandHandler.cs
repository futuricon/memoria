using MediatR;

using Memoria.Cards.Contracts.Queries;
using Memoria.Reminders.Contracts.Commands;
using Memoria.Reviews.Contracts.Commands;
using Memoria.Reviews.Contracts.Dtos;
using Memoria.Reviews.Contracts.Events;
using Memoria.Reviews.Domain;
using Memoria.Reviews.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Logging;

namespace Memoria.Reviews.Features.RecordReview;

internal sealed class RecordReviewCommandHandler
    : IRequestHandler<RecordReviewCommand, Result<ReviewDto>>
{
    private readonly ReviewsDbContext _db;
    private readonly IMediator _mediator;
    private readonly TimeProvider _clock;
    private readonly ILogger<RecordReviewCommandHandler> _logger;

    public RecordReviewCommandHandler(
        ReviewsDbContext db,
        IMediator mediator,
        TimeProvider clock,
        ILogger<RecordReviewCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _mediator = mediator;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<ReviewDto>> Handle(RecordReviewCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var cardResult = await _mediator.Send(
            new GetCardByIdQuery(request.UserId, request.CardId, IncludeDeleted: true),
            ct).ConfigureAwait(false);

        if (cardResult.IsFailure)
        {
            return Result<ReviewDto>.Failure(cardResult.Error!);
        }

        var card = cardResult.Value!;
        var now = _clock.GetUtcNow().UtcDateTime;

        var review = new Review(
            cardId: request.CardId,
            userId: request.UserId,
            reminderId: request.ReminderId,
            rating: request.Rating,
            cardTitleSnapshot: card.Title,
            reviewedAt: now,
            note: request.Note,
            answerText: request.AnswerText,
            aiScore: request.AiScore,
            aiFeedback: request.AiFeedback,
            autoGraded: request.AutoGraded);

        _db.Reviews.Add(review);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        if (request.ReminderId is { } reminderId)
        {
            var confirm = await _mediator.Send(
                new ConfirmReminderCommand(reminderId, request.UserId),
                ct).ConfigureAwait(false);

            if (confirm.IsFailure)
            {
                _logger.LogWarning(
                    "Review {ReviewId} recorded, but ConfirmReminder failed: {Code} {Message}",
                    review.Id, confirm.Error!.Code, confirm.Error.Message);
            }
        }

        // Drives adaptive rescheduling in the Reminders module. Published after
        // the reminder is confirmed so the subscriber sees a non-Pending state.
        await _mediator.Publish(
            new ReviewRecordedEvent(
                request.CardId, request.UserId, request.ReminderId, request.Rating, now),
            ct).ConfigureAwait(false);

        var dto = new ReviewDto(
            review.Id,
            review.CardId,
            review.UserId,
            review.ReminderId,
            review.Rating,
            review.CardTitleSnapshot,
            review.ReviewedAt,
            review.Note);

        return Result<ReviewDto>.Success(dto);
    }
}
