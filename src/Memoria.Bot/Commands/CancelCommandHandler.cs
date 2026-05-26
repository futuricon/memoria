using Memoria.Bot.Conversations;
using Memoria.Bot.Routing;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace Memoria.Bot.Commands;

internal sealed class CancelCommandHandler : ITextCommandHandler
{
    private readonly IConversationStateStore _conversations;
    private readonly ITelegramBotClient _client;

    public CancelCommandHandler(IConversationStateStore conversations, ITelegramBotClient client)
    {
        ArgumentNullException.ThrowIfNull(conversations);
        ArgumentNullException.ThrowIfNull(client);
        _conversations = conversations;
        _client = client;
    }

    public string CommandName => "cancel";

    public async Task HandleAsync(Message message, string? payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        var cleared = _conversations.Clear(message.Chat.Id);
        var text = cleared ? "❌ Cancelled." : "Nothing to cancel.";
        await _client.SendMessage(message.Chat.Id, text, cancellationToken: ct).ConfigureAwait(false);
    }
}
