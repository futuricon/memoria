using System.Globalization;

using MediatR;

using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Contracts.Queries;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Memoria.Bot.Callbacks;

internal sealed class DeleteConfirmCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;

    public DeleteConfirmCallbackHandler(
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

    public string Prefix => "del:";

    public async Task HandleAsync(CallbackQuery callback, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var data = callback.Data ?? string.Empty;
        if (callback.Message is not { } original)
        {
            await _client.AnswerCallbackQuery(callback.Id, cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        if (string.Equals(data, "del:cancel", StringComparison.Ordinal))
        {
            await _client.EditMessageText(
                original.Chat.Id, original.MessageId, "❌ Deletion cancelled.",
                replyMarkup: null, cancellationToken: ct).ConfigureAwait(false);
            await _client.AnswerCallbackQuery(callback.Id, cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        if (!data.StartsWith("del:confirm:", StringComparison.Ordinal))
        {
            await _client.AnswerCallbackQuery(callback.Id, "Bad callback", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var idN = data["del:confirm:".Length..];
        if (!Guid.TryParseExact(idN, "N", out var cardId))
        {
            await _client.AnswerCallbackQuery(callback.Id, "Bad id", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var resolved = await _resolver.ResolveAsync(callback.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            await _client.AnswerCallbackQuery(callback.Id, "Not linked", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var userId = resolved.Value!.UserId;
        var cardResult = await _mediator.Send(
            new GetCardByIdQuery(userId, cardId, IncludeDeleted: false), ct).ConfigureAwait(false);
        var title = cardResult.IsSuccess ? cardResult.Value!.Title : "(unknown)";

        var deleteResult = await _mediator.Send(
            new SoftDeleteCardCommand(userId, cardId), ct).ConfigureAwait(false);

        if (deleteResult.IsFailure)
        {
            await _client.EditMessageText(
                original.Chat.Id, original.MessageId,
                $"❌ {deleteResult.Error!.Message}",
                replyMarkup: null, cancellationToken: ct).ConfigureAwait(false);
        }
        else
        {
            var text =
                "🗑 Card deleted.\n\n" +
                $"\"{title}\" moved to trash. You can restore it within 90 days.";
            var restoreKeyboard = new InlineKeyboardMarkup(InlineKeyboardButton.WithCallbackData(
                "↩ Undo delete",
                $"card:restore:{cardId.ToString("N", CultureInfo.InvariantCulture)}"));
            await _client.EditMessageText(
                original.Chat.Id, original.MessageId, text, parseMode: ParseMode.Markdown,
                replyMarkup: restoreKeyboard, cancellationToken: ct).ConfigureAwait(false);
        }

        await _client.AnswerCallbackQuery(callback.Id, cancellationToken: ct).ConfigureAwait(false);
    }
}
