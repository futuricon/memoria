using MediatR;

namespace Memoria.Cards.Contracts.Events;

/// <summary>
/// Publish-after-save: soft-deleted карточка восстановлена. Слушают:
/// <c>Reminders</c> (для создания нового графика напоминаний от RestoredAt).
/// </summary>
public sealed record CardRestoredEvent(Guid CardId, Guid UserId, DateTime RestoredAt) : INotification;
