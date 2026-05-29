using MediatR;

using Memoria.Reviews.Contracts.Dtos;

namespace Memoria.Reviews.Contracts.Events;

/// <summary>
/// Published after a review is recorded (and the originating reminder, if any,
/// confirmed). The Reminders module subscribes to drive adaptive rescheduling:
/// the just-rated reminder's stage plus <see cref="Rating"/> determine the next
/// reminder. When <see cref="ReminderId"/> is <c>null</c> the review was not
/// tied to a reminder (e.g. an ad-hoc review) and no rescheduling occurs.
/// </summary>
public sealed record ReviewRecordedEvent(
    Guid CardId,
    Guid UserId,
    Guid? ReminderId,
    Rating Rating,
    DateTime ReviewedAt) : INotification;
