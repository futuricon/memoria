using MediatR;
using Memoria.Shared.Kernel.Results;
using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Reminders.Contracts.Commands;

public sealed record MarkReminderSentCommand(Guid ReminderId, int MessageId)
    : IRequest<Result<Unit>>;