using MediatR;
using Memoria.Reminders.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reminders.Contracts.Commands;

public sealed record RevealReminderAnswerCommand(Guid ReminderId, Guid UserId)
    : IRequest<Result<RevealedAnswerDto>>;