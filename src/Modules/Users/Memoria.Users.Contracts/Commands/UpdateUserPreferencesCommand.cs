using MediatR;

using Memoria.Shared.Kernel.Results;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Users.Contracts.Commands;

public sealed record UpdateUserPreferencesCommand(
    Guid UserId,
    string TimeZoneId,
    TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd) : IRequest<Result<Unit>>;
