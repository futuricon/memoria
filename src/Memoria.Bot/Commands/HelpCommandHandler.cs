using Memoria.Bot.Routing;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace Memoria.Bot.Commands;

internal sealed class HelpCommandHandler : ITextCommandHandler
{
    private const string HelpText =
        "🤖 Memoria — interval repetition bot\n" +
        "\n" +
        "Commands:\n" +
        "/add — create a new card\n" +
        "/list — show your cards\n" +
        "/card <id> — view one card\n" +
        "/delete <id> — delete a card\n" +
        "/tags — list your tags\n" +
        "/login — get a code to sign in to the SPA\n" +
        "/me — your profile\n" +
        "/timezone <IANA> — set timezone (e.g. /timezone Europe/Moscow)\n" +
        "/cancel — abort current dialog\n" +
        "/help — this message";

    private readonly ITelegramBotClient _client;

    public HelpCommandHandler(ITelegramBotClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public string CommandName => "help";

    public async Task HandleAsync(Message message, string? payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        await _client.SendMessage(message.Chat.Id, HelpText, cancellationToken: ct).ConfigureAwait(false);
    }
}
