using MediatR;

using Memoria.Shared.Kernel.Results;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Cards.Contracts.Commands;

/// <summary>
/// Resumes scheduling for a paused card. Creates a fresh reminder at the
/// stored <c>PausedAtStage</c> (or stage 1 if none was captured) with
/// <c>anchor=now</c> so the next interval starts ticking again.
/// </summary>
public sealed record UnpauseCardCommand(Guid UserId, Guid CardId) : IRequest<Result<Unit>>;
