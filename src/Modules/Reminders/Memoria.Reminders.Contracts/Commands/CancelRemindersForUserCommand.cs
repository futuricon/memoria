using MediatR;

using Memoria.Shared.Kernel.Results;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Reminders.Contracts.Commands;

/// <summary>
/// Bulk variant of <see cref="CancelRemindersForCardCommand"/>: cancels every
/// reminder owned by <paramref name="UserId"/> regardless of card or status.
/// Used by the account-merge flow to stop the Hangfire schedule for the
/// account that is about to be soft-deleted.
/// </summary>
public sealed record CancelRemindersForUserCommand(Guid UserId) : IRequest<Result<Unit>>;
