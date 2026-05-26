using Memoria.Bot.Conversations;

using Microsoft.Extensions.Logging;

using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Memoria.Bot.Routing;

/// <summary>
/// Центральный диспетчер Telegram-обновлений: парсит slash-команды, ведёт
/// пользователя через активный FSM-диалог, маршрутизирует callback-данные на
/// соответствующий <see cref="ICallbackHandler"/>.
/// </summary>
public sealed class BotMessageRouter
{
    private readonly Dictionary<string, ITextCommandHandler> _commands;
    private readonly List<ICallbackHandler> _callbacks;
    private readonly Dictionary<string, IConversationContinuationHandler> _continuations;
    private readonly IConversationStateStore _conversations;
    private readonly ILogger<BotMessageRouter> _logger;

    public BotMessageRouter(
        IEnumerable<ITextCommandHandler> commands,
        IEnumerable<ICallbackHandler> callbacks,
        IEnumerable<IConversationContinuationHandler> continuations,
        IConversationStateStore conversations,
        ILogger<BotMessageRouter> logger)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(callbacks);
        ArgumentNullException.ThrowIfNull(continuations);
        ArgumentNullException.ThrowIfNull(conversations);
        ArgumentNullException.ThrowIfNull(logger);

        _commands = commands.ToDictionary(h => h.CommandName, StringComparer.OrdinalIgnoreCase);
        _callbacks = callbacks.ToList();
        _continuations = continuations.ToDictionary(h => h.DialogName, StringComparer.Ordinal);
        _conversations = conversations;
        _logger = logger;
    }

    public async Task RouteAsync(Update update, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(update);

        if (update.Message is { } message)
        {
            await RouteMessageAsync(message, ct).ConfigureAwait(false);
            return;
        }

        if (update.CallbackQuery is { } callback)
        {
            await RouteCallbackAsync(callback, ct).ConfigureAwait(false);
            return;
        }

        _logger.LogDebug("Unsupported update type {Type}, ignoring", update.Type);
    }

    private async Task RouteMessageAsync(Message message, CancellationToken ct)
    {
        if (message.Type != MessageType.Text || string.IsNullOrWhiteSpace(message.Text))
        {
            _logger.LogDebug("Non-text message in chat {ChatId}, ignoring", message.Chat.Id);
            return;
        }

        var text = message.Text;
        if (text.StartsWith('/'))
        {
            var (cmd, payload) = ParseCommand(text);
            if (_commands.TryGetValue(cmd, out var handler))
            {
                await handler.HandleAsync(message, payload, ct).ConfigureAwait(false);
            }
            else
            {
                _logger.LogDebug("Unknown command '/{Cmd}' in chat {ChatId}", cmd, message.Chat.Id);
            }
            return;
        }

        if (_conversations.TryGet(message.Chat.Id, out var state) && state is not null)
        {
            if (_continuations.TryGetValue(state.DialogName, out var continuation))
            {
                await continuation.HandleAsync(message, state, ct).ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning(
                    "Active state with DialogName '{Dialog}' has no continuation handler", state.DialogName);
            }
            return;
        }

        _logger.LogDebug("Unrecognized non-command text in chat {ChatId}", message.Chat.Id);
    }

    private async Task RouteCallbackAsync(CallbackQuery callback, CancellationToken ct)
    {
        var data = callback.Data ?? string.Empty;
        var handler = _callbacks.FirstOrDefault(h => data.StartsWith(h.Prefix, StringComparison.Ordinal));
        if (handler is null)
        {
            _logger.LogWarning("No callback handler matches data prefix '{Data}'", data);
            return;
        }

        await handler.HandleAsync(callback, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Разбирает первую "слово/пайлоад" пару из строки <c>/cmd[@bot] [payload...]</c>.
    /// Если за командой следует <c>@bot_username</c>, имя бота отбрасывается.
    /// </summary>
    internal static (string Cmd, string? Payload) ParseCommand(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var body = text.TrimStart('/');
        var firstSpace = body.IndexOfAny([' ', '\n', '\t']);
        string commandPart;
        string? payload;
        if (firstSpace < 0)
        {
            commandPart = body;
            payload = null;
        }
        else
        {
            commandPart = body[..firstSpace];
            payload = body[(firstSpace + 1)..].TrimStart();
            if (payload.Length == 0) payload = null;
        }

        var at = commandPart.IndexOf('@', StringComparison.Ordinal);
        if (at >= 0) commandPart = commandPart[..at];

        return (commandPart.ToLowerInvariant(), payload);
    }
}
