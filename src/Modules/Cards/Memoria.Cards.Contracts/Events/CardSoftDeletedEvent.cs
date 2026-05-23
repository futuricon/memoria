using MediatR;

namespace Memoria.Cards.Contracts.Events;

/// <summary>
/// Publish-after-save: карточка soft-deleted. Слушают: <c>Reminders</c>
/// (для отмены оставшихся pending-напоминаний и hangfire-задач).
/// </summary>
public sealed record CardSoftDeletedEvent(Guid CardId, Guid UserId, DateTime DeletedAt) : INotification;
