using MediatR;

using Memoria.Shared.Kernel.Results;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Cards.Contracts.Commands;

/// <summary>
/// Помечает карточку как удалённую (<c>DeletedAt = UtcNow</c>),
/// удаляет pending-напоминания, публикует <c>CardSoftDeletedEvent</c>.
/// </summary>
public sealed record SoftDeleteCardCommand(Guid UserId, Guid CardId) : IRequest<Result<Unit>>;
