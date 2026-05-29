using System.Globalization;

using MediatR;

using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Cards.Contracts.Queries;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Memoria.Bot.Commands;

internal sealed class ListCommandHandler : ITextCommandHandler
{
    internal const int PageSize = 10;

    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;

    public ListCommandHandler(
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

    public string CommandName => "list";

    public async Task HandleAsync(Message message, string? payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.From is null) return;

        var resolved = await _resolver.ResolveAsync(message.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            await _client.SendMessage(
                message.Chat.Id, "❌ Not linked yet.", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var page = 1;
        if (int.TryParse(payload, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPage))
        {
            page = Math.Max(1, parsedPage);
        }

        await SendPageAsync(message.Chat.Id, resolved.Value!.UserId, page, editMessageId: null, ct).ConfigureAwait(false);
    }

    internal async Task SendPageAsync(long chatId, Guid userId, int page, int? editMessageId, CancellationToken ct)
    {
        var listResult = await _mediator.Send(
            new ListCardsQuery(userId, Search: null, Tags: null, page, PageSize), ct).ConfigureAwait(false);

        if (listResult.IsFailure)
        {
            await _client.SendMessage(
                chatId, $"❌ {listResult.Error!.Message}", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var paged = listResult.Value!;
        string text;
        InlineKeyboardMarkup? keyboard = null;

        if (paged.Items.Count == 0)
        {
            text = "📚 You have no cards yet.";
            keyboard = new InlineKeyboardMarkup(
                InlineKeyboardButton.WithCallbackData("➕ Add card", "menu:add"));
        }
        else
        {
            var totalPages = Math.Max(1, (paged.TotalCount + PageSize - 1) / PageSize);
            var lines = paged.Items.Select(c =>
                $"`{c.Id.ToString("N", CultureInfo.InvariantCulture)[..8]}` {c.Title}");
            text = $"📚 Cards (page {page} of {totalPages}):\n\n" + string.Join("\n", lines);
            keyboard = BuildPaginationKeyboard(page, paged.TotalCount);
        }

        if (editMessageId is { } msgId)
        {
            await _client.EditMessageText(
                chatId, msgId, text, parseMode: ParseMode.Markdown,
                replyMarkup: keyboard, cancellationToken: ct).ConfigureAwait(false);
        }
        else
        {
            await _client.SendMessage(
                chatId, text, parseMode: ParseMode.Markdown,
                replyMarkup: keyboard, cancellationToken: ct).ConfigureAwait(false);
        }
    }

    private static InlineKeyboardMarkup? BuildPaginationKeyboard(int page, int totalCount)
    {
        var buttons = new List<InlineKeyboardButton>();
        if (page > 1)
        {
            buttons.Add(InlineKeyboardButton.WithCallbackData("◀ Prev",
                $"list:prev:{page.ToString(CultureInfo.InvariantCulture)}"));
        }
        if (page * PageSize < totalCount)
        {
            buttons.Add(InlineKeyboardButton.WithCallbackData("Next ▶",
                $"list:next:{page.ToString(CultureInfo.InvariantCulture)}"));
        }

        return buttons.Count == 0 ? null : new InlineKeyboardMarkup(buttons);
    }
}
