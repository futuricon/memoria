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

internal sealed class CardCommandHandler : ITextCommandHandler
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;
    private readonly CardIdResolver _idResolver;

    public CardCommandHandler(
        ITelegramBotClient client,
        IMediator mediator,
        CurrentUserResolver resolver,
        CardIdResolver idResolver)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(idResolver);
        _client = client;
        _mediator = mediator;
        _resolver = resolver;
        _idResolver = idResolver;
    }

    public string CommandName => "card";

    public async Task HandleAsync(Message message, string? payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.From is null) return;

        if (string.IsNullOrWhiteSpace(payload))
        {
            await _client.SendMessage(
                message.Chat.Id, "Usage: /card <8-char-id>", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var resolved = await _resolver.ResolveAsync(message.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            await _client.SendMessage(
                message.Chat.Id, "❌ Not linked yet.", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var userId = resolved.Value!.UserId;
        var idResult = await _idResolver.ResolveAsync(userId, payload.Trim(), ct).ConfigureAwait(false);
        if (idResult.IsFailure)
        {
            await _client.SendMessage(
                message.Chat.Id, $"❌ {idResult.Error!.Message}", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var cardResult = await _mediator.Send(
            new GetCardByIdQuery(userId, idResult.Value, IncludeDeleted: false), ct).ConfigureAwait(false);

        if (cardResult.IsFailure)
        {
            await _client.SendMessage(
                message.Chat.Id, $"❌ {cardResult.Error!.Message}", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var card = cardResult.Value!;
        var tagsLine = card.Tags.Count > 0 ? string.Join(" ", card.Tags.Select(t => "#" + t)) : "—";

        var text =
            $"*{Escape(card.Title)}*\n\n" +
            $"{Escape(card.Body)}\n\n" +
            $"🏷 {tagsLine}\n" +
            $"📅 Created: {card.CreatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";

        var idN = card.Id.ToString("N", CultureInfo.InvariantCulture);
        var keyboard = new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData("🗑 Delete", $"del:confirm:{idN}"));

        await _client.SendMessage(
            message.Chat.Id, text, parseMode: ParseMode.Markdown,
            replyMarkup: keyboard, cancellationToken: ct).ConfigureAwait(false);
    }

    internal static string Escape(string s) =>
        s
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);
}
