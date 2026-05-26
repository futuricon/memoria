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
/// </summary>
public sealed record RecordReviewCommand(
    Guid UserId,
    Guid CardId,
    Guid? ReminderId,
    Rating Rating,
    string? Note) : IRequest<Result<ReviewDto>>;
