using MediatR;

namespace Memoria.Cards.Contracts.Events;

/// <summary>
/// Publish-after-save: новая карточка создана и сохранена в БД.
/// Slушают: <c>Reminders</c> (для создания графика напоминаний).
/// </summary>
public sealed record CardCreatedEvent(Guid CardId, Guid UserId, DateTime CreatedAt) : INotification;
