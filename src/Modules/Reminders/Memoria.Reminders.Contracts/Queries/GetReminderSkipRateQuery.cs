using MediatR;

using Memoria.Reminders.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reminders.Contracts.Queries;

public sealed record GetReminderSkipRateQuery(int DaysBack = 30)
    : IRequest<Result<ReminderSkipRateDto>>;
