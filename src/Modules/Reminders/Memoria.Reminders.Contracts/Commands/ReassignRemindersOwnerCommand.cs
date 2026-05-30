using MediatR;

using Memoria.Shared.Kernel.Results;

namespace Memoria.Reminders.Contracts.Commands;

/// <summary>
/// Bulk-updates <c>Reminder.UserId</c> from <paramref name="SourceUserId"/>
/// to <paramref name="TargetUserId"/> across all statuses. Idempotent — a
/// re-run after a partial failure simply matches no rows.
/// Returns the number of affected rows.
/// </summary>
public sealed record ReassignRemindersOwnerCommand(
    Guid SourceUserId,
    Guid TargetUserId) : IRequest<Result<int>>;
