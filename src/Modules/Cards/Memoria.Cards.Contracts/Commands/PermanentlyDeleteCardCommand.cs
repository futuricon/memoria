using MediatR;

using Memoria.Shared.Kernel.Results;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Cards.Contracts.Commands;

/// <summary>
/// Физически удаляет карточку. Допустимо только для уже soft-deleted карточек
/// (<c>DeletedAt != null</c>). Удаляет все <c>Reminder</c> и <c>CardTag</c>,
/// но <c>Review</c>-записи сохраняются благодаря <c>CardTitleSnapshot</c>.
/// </summary>
public sealed record PermanentlyDeleteCardCommand(Guid UserId, Guid CardId) : IRequest<Result<Unit>>;
