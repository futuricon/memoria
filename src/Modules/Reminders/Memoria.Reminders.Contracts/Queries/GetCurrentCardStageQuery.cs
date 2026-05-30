using MediatR;

using Memoria.Shared.Kernel.Results;

namespace Memoria.Reminders.Contracts.Queries;

/// <summary>
/// Returns the highest <c>StageNumber</c> among any reminders that exist for
/// this card, regardless of status. <c>null</c> if the card has never had a
/// reminder. Used by the Cards module's Pause flow to snapshot where the user
/// left off so Unpause can resume from the same stage.
/// </summary>
public sealed record GetCurrentCardStageQuery(Guid CardId) : IRequest<Result<int?>>;
