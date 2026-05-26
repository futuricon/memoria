using MediatR;

using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Contracts.Queries;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace Memoria.Bot.Commands;

internal sealed class TimezoneCommandHandler : ITextCommandHandler
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;

    public TimezoneCommandHandler(
        ITelegramBotClient client,
        IMediator mediator,
        CurrentUserResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(resolver);
        _client = client;
        _mediator = mediator;
        _resolver = resolver;
    }

    public string CommandName => "timezone";

    public async Task HandleAsync(Message message, string? payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.From is null) return;

        if (string.IsNullOrWhiteSpace(payload))
        {
            await _client.SendMessage(
                message.Chat.Id,
                "Usage: /timezone <IANA> (e.g. /timezone Europe/Moscow)",
                cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var tz = payload.Trim();
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(tz);
        }
        catch (TimeZoneNotFoundException)
        {
            await _client.SendMessage(
                message.Chat.Id,
                $"❌ Unknown timezone '{tz}'. Use an IANA id like Europe/Moscow.",
                cancellationToken: ct).ConfigureAwait(false);
            return;
        }
        catch (InvalidTimeZoneException)
        {
            await _client.SendMessage(
                message.Chat.Id,
                $"❌ Timezone '{tz}' has invalid data.",
                cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var resolved = await _resolver.ResolveAsync(message.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            await _client.SendMessage(
                message.Chat.Id, "❌ Not linked yet.", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var current = await _mediator.Send(
            new GetUserPreferencesQuery(resolved.Value!.UserId), ct).ConfigureAwait(false);
        var (quietStart, quietEnd) = current.IsSuccess
            ? (current.Value!.QuietHoursStart, current.Value!.QuietHoursEnd)
            : (default(TimeOnly?), default(TimeOnly?));

        var update = await _mediator.Send(
            new UpdateUserPreferencesCommand(resolved.Value.UserId, tz, quietStart, quietEnd), ct).ConfigureAwait(false);

        var reply = update.IsSuccess
            ? $"✅ Timezone set to {tz}."
            : $"❌ {update.Error!.Message}";
        await _client.SendMessage(message.Chat.Id, reply, cancellationToken: ct).ConfigureAwait(false);
    }
}
