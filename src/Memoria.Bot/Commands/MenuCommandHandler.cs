using Memoria.Bot.Localization;
using Memoria.Bot.Routing;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Memoria.Bot.Commands;

/// <summary>
/// <c>/menu</c> — sends the inline home screen. Navigation and actions are
/// handled by <c>MenuCallbackHandler</c> (prefix <c>menu:</c>).
/// </summary>
internal sealed class MenuCommandHandler : ITextCommandHandler
{
    private readonly ITelegramBotClient _client;

    public MenuCommandHandler(ITelegramBotClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    public string CommandName => "menu";

    public async Task HandleAsync(Message message, string? payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _client.SendMessage(
            message.Chat.Id,
            BotText.MenuTitle,
            parseMode: ParseMode.Markdown,
            replyMarkup: HomeKeyboard,
            cancellationToken: ct).ConfigureAwait(false);
    }

    /// <summary>Home inline keyboard, reused by <c>MenuCallbackHandler</c> for "back".</summary>
    public static InlineKeyboardMarkup HomeKeyboard { get; } = new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData(BotText.BtnAdd, "menu:add"),
            InlineKeyboardButton.WithCallbackData(BotText.BtnList, "menu:list"),
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData(BotText.BtnDue, "menu:due"),
            InlineKeyboardButton.WithCallbackData(BotText.BtnTags, "menu:tags"),
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData(BotText.BtnSettings, "menu:settings"),
            InlineKeyboardButton.WithCallbackData(BotText.BtnHelp, "menu:help"),
        },
    });
}
