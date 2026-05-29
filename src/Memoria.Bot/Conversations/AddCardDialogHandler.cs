using Memoria.Bot.Services;
using Memoria.Cards.Contracts;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Memoria.Bot.Conversations;

/// <summary>
/// Обрабатывает текстовые сообщения, приходящие в активном FSM-диалоге
/// <see cref="AddCardDialogState"/> (то есть пользователь ввёл <c>/add</c> без
/// payload и сейчас отвечает по шагам). Слаш-команды до этого handler-а не
/// доходят: их перехватывает <see cref="Routing.BotMessageRouter"/>.
/// </summary>
internal sealed class AddCardDialogHandler : IConversationContinuationHandler
{
    private const int MaxTitleLength = CardConstraints.MaxTitleLength;
    private const int MaxBodyLength = CardConstraints.MaxBodyLength;

    private readonly ITelegramBotClient _client;
    private readonly IConversationStateStore _conversations;

    public AddCardDialogHandler(ITelegramBotClient client, IConversationStateStore conversations)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(conversations);
        _client = client;
        _conversations = conversations;
    }

    public string DialogName => "AddCard";

    public Task HandleAsync(Message message, ConversationState state, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(state);

        if (state is not AddCardDialogState addState)
        {
            return Task.CompletedTask;
        }

        var text = message.Text ?? string.Empty;
        return addState.Step switch
        {
            AddCardStep.WaitingForTitle => HandleTitleAsync(message, addState, text, ct),
            AddCardStep.WaitingForBody => HandleBodyAsync(message, addState, text, ct),
            AddCardStep.WaitingForTags => HandleTagsAsync(message, addState, text, ct),
            AddCardStep.Preview => RepromptPreviewAsync(message, ct),
            _ => Task.CompletedTask,
        };
    }

    private async Task HandleTitleAsync(Message message, AddCardDialogState state, string text, CancellationToken ct)
    {
        var title = text.Trim();
        if (title.Length == 0 || title.Length > MaxTitleLength)
        {
            await _client.SendMessage(
                message.Chat.Id,
                $"❌ Title must be 1..{MaxTitleLength} characters. Try again:",
                cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        _conversations.Update(message.Chat.Id, state with { Step = AddCardStep.WaitingForBody, Title = title });
        await _client.SendMessage(
            message.Chat.Id, "📝 Now enter card body:", cancellationToken: ct).ConfigureAwait(false);
    }

    private async Task HandleBodyAsync(Message message, AddCardDialogState state, string text, CancellationToken ct)
    {
        var body = text.Trim();
        if (body.Length == 0 || body.Length > MaxBodyLength)
        {
            await _client.SendMessage(
                message.Chat.Id,
                $"❌ Body must be 1..{MaxBodyLength} characters. Try again:",
                cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        _conversations.Update(message.Chat.Id, state with { Step = AddCardStep.WaitingForTags, Body = body });
        await _client.SendMessage(
            message.Chat.Id,
            "🏷 Enter tags separated by space (e.g., #postgres #database) or 'skip' for no tags:",
            cancellationToken: ct).ConfigureAwait(false);
    }

    private async Task HandleTagsAsync(Message message, AddCardDialogState state, string text, CancellationToken ct)
    {
        IReadOnlyList<string> tags;
        if (text.Trim().Equals("skip", StringComparison.OrdinalIgnoreCase))
        {
            tags = Array.Empty<string>();
        }
        else
        {
            var (_, extracted) = AddCardParser.ExtractTags(text);
            tags = extracted
                .Select(t => t.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        var nextState = state with { Step = AddCardStep.Preview, Tags = tags };
        _conversations.Update(message.Chat.Id, nextState);
        await SendPreviewAsync(message.Chat.Id, nextState, ct).ConfigureAwait(false);
    }

    private async Task RepromptPreviewAsync(Message message, CancellationToken ct)
    {
        await _client.SendMessage(
            message.Chat.Id,
            "Use the buttons below the preview to confirm or cancel.",
            cancellationToken: ct).ConfigureAwait(false);
    }

    private async Task SendPreviewAsync(long chatId, AddCardDialogState state, CancellationToken ct)
    {
        var tagsLine = state.Tags is { Count: > 0 } t
            ? "🏷 " + string.Join(" ", t.Select(x => "#" + x))
            : "🏷 —";
        var preview =
            "📋 Preview:\n\n" +
            $"*{Escape(state.Title ?? string.Empty)}*\n" +
            Escape(state.Body ?? string.Empty) + "\n" +
            tagsLine;

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Create", "addcard:confirm"),
                InlineKeyboardButton.WithCallbackData("❌ Cancel", "addcard:cancel"),
            },
        });

        await _client.SendMessage(
            chatId, preview, parseMode: ParseMode.Markdown,
            replyMarkup: keyboard, cancellationToken: ct).ConfigureAwait(false);
    }

    private static string Escape(string s) =>
        s
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);
}
