using MediatR;
using Memoria.Shared.Kernel.Results;
using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Reminders.Contracts.Commands;

/// <summary>
/// Schedules the next reminder for a card. <paramref name="Stage"/> = <c>null</c>
/// means "fresh card, start at stage 1"; a value means "resume from this
/// stage" (used by the unpause flow to thaw a card back at the same stage).
/// </summary>
public sealed record ScheduleRemindersForCardCommand(
    Guid CardId,
    Guid UserId,
    DateTime AnchorUtc,
    int? Stage = null) : IRequest<Result<Unit>>;