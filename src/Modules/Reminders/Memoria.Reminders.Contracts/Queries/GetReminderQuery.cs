using MediatR;

using Memoria.Reminders.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reminders.Contracts.Queries;

public sealed record GetReminderQuery(Guid ReminderId, Guid UserId)
    : IRequest<Result<ReminderDto>>;
