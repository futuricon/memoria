using MediatR;

using Memoria.Reminders.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reminders.Contracts.Queries;

/// <summary>
/// Pending-напоминания пользователя, попадающие в указанный календарный
/// день в TZ этого пользователя (берётся из <c>GetUserPreferencesQuery</c>).
/// </summary>
public sealed record GetDueRemindersForUserQuery(Guid UserId, DateOnly Date)
    : IRequest<Result<IReadOnlyList<DueReminderDto>>>;
