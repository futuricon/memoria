using MediatR;

using Memoria.Reminders.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reminders.Contracts.Queries;

/// <summary>
/// Returns the next <paramref name="Take"/> pending reminders for a user
/// ordered by <c>ScheduledAt</c>. Unlike <see cref="GetDueRemindersForUserQuery"/>,
/// this is unbounded by day — it powers the SPA dashboard "what's coming up"
/// widget which deliberately spans into the future.
/// </summary>
public sealed record GetUpcomingRemindersForUserQuery(Guid UserId, int Take = 10)
    : IRequest<Result<IReadOnlyList<DueReminderDto>>>;
