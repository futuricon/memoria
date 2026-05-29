using System.Globalization;

using MediatR;

using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Queries;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Memoria.Bot.Commands;

/// <summary>
/// <c>/list</c> — renders the inline card browser: each card is a button (5 per
/// page) with ◀/▶ navigation below. Tapping a card opens its detail view —
/// see <c>CardsBrowseCallbackHandler</c> (prefix <c>cards:</c>).
/// </summary>
internal sealed class ListCommandHandler : ITextCommandHandler
{
    internal const int PageSize = 5;
    private const int MaxTitleOnButton = 40;

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
        InlineKeyboardMarkup keyboard;

        if (paged.Items.Count == 0)
        {
            text = "📚 You have no cards yet.";
            keyboard = new InlineKeyboardMarkup(
                InlineKeyboardButton.WithCallbackData("➕ Add card", "menu:add"));
        }
        else
        {
            var totalPages = Math.Max(1, (paged.TotalCount + PageSize - 1) / PageSize);
            text = $"📚 *Your cards* — page {page.ToString(CultureInfo.InvariantCulture)} of " +
                   $"{totalPages.ToString(CultureInfo.InvariantCulture)}";
            keyboard = BuildCardsKeyboard(paged.Items, page, paged.TotalCount);
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

    private static InlineKeyboardMarkup BuildCardsKeyboard(
        IReadOnlyList<CardSummaryDto> items, int page, int totalCount)
    {
        var rows = new List<InlineKeyboardButton[]>(items.Count + 2);

        foreach (var card in items)
        {
            var id = card.Id.ToString("N", CultureInfo.InvariantCulture);
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    Truncate(card.Title),
                    $"cards:open:{page.ToString(CultureInfo.InvariantCulture)}:{id}"),
            });
        }

        var nav = new List<InlineKeyboardButton>(2);
        if (page > 1)
        {
            nav.Add(InlineKeyboardButton.WithCallbackData(
                "◀ Back", $"cards:page:{(page - 1).ToString(CultureInfo.InvariantCulture)}"));
        }
        if (page * PageSize < totalCount)
        {
            nav.Add(InlineKeyboardButton.WithCallbackData(
                "Forward ▶", $"cards:page:{(page + 1).ToString(CultureInfo.InvariantCulture)}"));
        }
        if (nav.Count > 0)
        {
            rows.Add(nav.ToArray());
        }

        rows.Add(new[] { InlineKeyboardButton.WithCallbackData("🏠 Menu", "menu:home") });

        return new InlineKeyboardMarkup(rows);
    }

    private static string Truncate(string s) =>
        s.Length <= MaxTitleOnButton ? s : s[..(MaxTitleOnButton - 1)] + "…";
}
