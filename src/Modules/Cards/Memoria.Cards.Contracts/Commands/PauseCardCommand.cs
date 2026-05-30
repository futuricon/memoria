using MediatR;

using Memoria.Shared.Kernel.Results;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Cards.Contracts.Commands;

/// <summary>
/// Suspends scheduling for a card: cancels any pending reminders and stores
/// the highest stage seen so far on <c>Card.PausedAtStage</c> so the user can
/// pick up where they left off after <see cref="UnpauseCardCommand"/>.
/// </summary>
public sealed record PauseCardCommand(Guid UserId, Guid CardId) : IRequest<Result<Unit>>;
