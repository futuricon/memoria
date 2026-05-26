using MediatR;
using Memoria.Shared.Kernel.Results;
using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Reminders.Contracts.Commands;

public sealed record MarkReminderFailedCommand(Guid ReminderId, string Reason)
    : IRequest<Result<Unit>>;