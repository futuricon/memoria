using System.Globalization;

using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;

namespace Memoria.Bot.Services;

/// <summary>
/// Резолвит "текущего пользователя" по Telegram chat id одной MediatR-командой.
/// Каждый command/callback-handler в боте вызывает <see cref="ResolveAsync"/>
/// перед делегированием в Users/Cards/Reminders.
/// </summary>
internal sealed class CurrentUserResolver
{
    private readonly IMediator _mediator;

    public CurrentUserResolver(IMediator mediator)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        _mediator = mediator;
    }

    public Task<Result<UserIdentityResolutionDto>> ResolveAsync(long telegramId, CancellationToken ct)
    {
        return _mediator.Send(
            new GetUserByTelegramIdQuery(telegramId.ToString(CultureInfo.InvariantCulture)),
            ct);
    }
}
