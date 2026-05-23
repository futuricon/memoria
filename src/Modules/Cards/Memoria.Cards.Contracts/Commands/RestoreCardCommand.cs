using MediatR;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Contracts.Commands;

/// <summary>
/// Восстанавливает soft-deleted карточку из «корзины». Сбрасывает
/// <c>DeletedAt</c>, пересоздаёт график напоминаний от <c>UtcNow</c>,
/// публикует <c>CardRestoredEvent</c>. Допустимо только в течение
/// 90-дневного grace period.
/// </summary>
public sealed record RestoreCardCommand(Guid UserId, Guid CardId) : IRequest<Result<CardDto>>;
