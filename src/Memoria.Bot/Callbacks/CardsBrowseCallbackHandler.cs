using System.Globalization;

using MediatR;

using Memoria.Bot.Commands;
using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Cards.Contracts.Queries;

using Microsoft.Extensions.Logging;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Memoria.Bot.Callbacks;

/// <summary>
/// Inline card browser (prefix <c>cards:</c>), editing one message in place:
/// <list type="bullet">
///   <item><c>cards:page:&lt;n&gt;</c> — render list page n (card buttons + nav).</item>
///   <item><c>cards:open:&lt;page&gt;:&lt;id32&gt;</c> — card detail with Delete + Back-to-list.</item>
/// </list>
/// </summary>
internal sealed class CardsBrowseCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;
    private readonly ListCommandHandler _list;
    private readonly ILogger<CardsBrowseCallbackHandler> _logger;

    public CardsBrowseCallbackHandler(
        ITelegramBotClient client,
        IMediator mediator,
        CurrentUserResolver resolver,
        ListCommandHandler list,
        ILogger<CardsBrowseCallbackHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _mediator = mediator;
        _resolver = resolver;
        _list = list;
        _logger = logger;
    }

    public string Prefix => "cards:";

    public async Task HandleAsync(CallbackQuery callback, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (callback.Message is not { } origin)
        {
            await Answer(callback, null, ct).ConfigureAwait(false);
            return;
        }

        var parts = (callback.Data ?? string.Empty).Split(':');
        var action = parts.Length > 1 ? parts[1] : string.Empty;

        var resolved = await _resolver.ResolveAsync(callback.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            await Answer(callback, "Not linked", ct).ConfigureAwait(false);
            return;
        }

        var userId = resolved.Value!.UserId;

        switch (action)
        {
            case "page" when parts.Length == 3
                             && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var page):
                await _list.SendPageAsync(origin.Chat.Id, userId, Math.Max(1, page), origin.MessageId, ct)
                    .ConfigureAwait(false);
                await Answer(callback, null, ct).ConfigureAwait(false);
                break;

            case "open" when parts.Length == 4
                             && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fromPage)
                             && Guid.TryParseExact(parts[3], "N", out var cardId):
                await ShowCardAsync(callback, origin, userId, cardId, Math.Max(1, fromPage), ct).ConfigureAwait(false);
                break;

            default:
                await Answer(callback, "Bad action", ct).ConfigureAwait(false);
                break;
        }
    }

    private async Task ShowCardAsync(
        CallbackQuery callback, Message origin, Guid userId, Guid cardId, int fromPage, CancellationToken ct)
    {
        var cardResult = await _mediator.Send(
            new GetCardByIdQuery(userId, cardId, IncludeDeleted: false), ct).ConfigureAwait(false);
        if (cardResult.IsFailure)
        {
            await Answer(callback, cardResult.Error!.Message, ct).ConfigureAwait(false);
            return;
        }

        var card = cardResult.Value!;
        var tagsLine = card.Tags.Count > 0 ? string.Join(" ", card.Tags.Select(t => "#" + t)) : "—";
        var text =
            $"*{CardCommandHandler.Escape(card.Title)}*\n\n" +
            $"{CardCommandHandler.Escape(card.Body)}\n\n" +
            $"🏷 {tagsLine}\n" +
            $"📅 Created: {card.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

        var idN = card.Id.ToString("N", CultureInfo.InvariantCulture);
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🗑 Delete", $"del:confirm:{idN}") },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "◀ Back to list", $"cards:page:{fromPage.ToString(CultureInfo.InvariantCulture)}"),
            },
        });

        try
        {
            await _client.EditMessageText(
                origin.Chat.Id, origin.MessageId, text,
                parseMode: ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to render card {CardId} in browser", cardId);
        }

        await Answer(callback, null, ct).ConfigureAwait(false);
    }

    private async Task Answer(CallbackQuery callback, string? text, CancellationToken ct)
    {
        try
        {
            await _client.AnswerCallbackQuery(callback.Id, text, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to answer callback {CallbackId}", callback.Id);
        }
    }
}
