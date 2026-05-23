using MediatR;

namespace Memoria.Cards.Contracts.Events;

/// <summary>
/// Publish-after-save: карточка физически удалена. Слушают: <c>Reminders</c>
/// (на случай если остались записи). <c>Review</c>-записи трогаем НЕ —
/// они выживают за счёт <c>CardTitleSnapshot</c>.
/// </summary>
public sealed record CardPermanentlyDeletedEvent(Guid CardId, Guid UserId) : INotification;
