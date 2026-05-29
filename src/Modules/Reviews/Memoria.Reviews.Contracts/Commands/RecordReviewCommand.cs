using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reviews.Contracts.Commands;

/// <summary>
/// Records a review after the user rated a reminder. Snapshots
/// <c>Card.Title</c> into <see cref="ReviewDto.CardTitleSnapshot"/> at
/// creation. If <paramref name="ReminderId"/> is non-null, the
/// corresponding reminder is transitioned to <c>Confirmed</c> via
/// <c>ConfirmReminderCommand</c>.
/// <para>
/// The optional AI fields are populated when a Question card was auto-graded:
/// <paramref name="AnswerText"/> is the user's submitted answer,
/// <paramref name="AiScore"/>/<paramref name="AiFeedback"/> the grade, and
/// <paramref name="AutoGraded"/> indicates the <paramref name="Rating"/> was
/// derived from that grade rather than chosen by the user.
/// </para>
/// </summary>
public sealed record RecordReviewCommand(
    Guid UserId,
    Guid CardId,
    Guid? ReminderId,
    Rating Rating,
    string? Note,
    string? AnswerText = null,
    int? AiScore = null,
    string? AiFeedback = null,
    bool AutoGraded = false) : IRequest<Result<ReviewDto>>;
