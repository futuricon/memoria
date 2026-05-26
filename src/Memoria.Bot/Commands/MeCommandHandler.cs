using System.Globalization;
using System.Text;

using MediatR;

using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Users.Contracts.Queries;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace Memoria.Bot.Commands;

internal sealed class MeCommandHandler : ITextCommandHandler
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;

    public MeCommandHandler(
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

    public string CommandName => "me";

    public async Task HandleAsync(Message message, string? payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.From is null) return;

        var resolved = await _resolver.ResolveAsync(message.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            await _client.SendMessage(
                message.Chat.Id,
                "❌ Not linked yet. Use the bot from your SPA registration flow.",
                cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var user = resolved.Value!;

        var prefs = await _mediator.Send(new GetUserPreferencesQuery(user.UserId), ct).ConfigureAwait(false);
        var identities = await _mediator.Send(new GetUserIdentitiesQuery(user.UserId), ct).ConfigureAwait(false);

        var sb = new StringBuilder();
        sb.Append("👤 ").AppendLine(user.DisplayName);
        sb.Append("📧 ").AppendLine(user.Email ?? "-");

        if (prefs.IsSuccess)
        {
            var p = prefs.Value!;
            sb.Append("🌍 Timezone: ").AppendLine(p.TimeZoneId);
            var quiet = (p.QuietHoursStart is { } s && p.QuietHoursEnd is { } e)
                ? string.Create(CultureInfo.InvariantCulture, $"{s:HH:mm}–{e:HH:mm}")
                : "-";
            sb.Append("🌙 Quiet hours: ").AppendLine(quiet);
        }

        if (identities.IsSuccess && identities.Value!.Count > 0)
        {
            var providers = string.Join(", ", identities.Value!.Select(i => i.Provider));
            sb.Append("🔗 Linked: ").AppendLine(providers);
        }

        await _client.SendMessage(message.Chat.Id, sb.ToString(), cancellationToken: ct).ConfigureAwait(false);
    }
}
