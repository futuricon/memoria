using MediatR;

using Memoria.Shared.Kernel.Results;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Reminders.Contracts.Commands;

/// <summary>
/// Delivers a still-PENDING reminder immediately — e.g. the user tapped
/// "review now" in <c>/due</c> instead of waiting for the scheduled time. The
/// normal send job is enqueued; the originally scheduled run will later find the
/// reminder non-pending and skip (idempotent). Fails if the reminder is missing,
/// not owned by the user, or already delivered/handled.
/// </summary>
public sealed record DeliverReminderNowCommand(Guid ReminderId, Guid UserId) : IRequest<Result<Unit>>;
