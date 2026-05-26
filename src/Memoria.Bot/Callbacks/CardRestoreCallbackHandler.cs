using MediatR;

using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Cards.Contracts.Commands;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace Memoria.Bot.Callbacks;

internal sealed class CardRestoreCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;

    public CardRestoreCallbackHandler(
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

    public string Prefix => "card:";

    public async Task HandleAsync(CallbackQuery callback, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var data = callback.Data ?? string.Empty;
        if (!data.StartsWith("card:restore:", StringComparison.Ordinal))
        {
            await _client.AnswerCallbackQuery(callback.Id, "Bad callback", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var idN = data["card:restore:".Length..];
        if (!Guid.TryParseExact(idN, "N", out var cardId))
        {
            await _client.AnswerCallbackQuery(callback.Id, "Bad id", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        if (callback.Message is not { } original)
        {
            await _client.AnswerCallbackQuery(callback.Id, cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var resolved = await _resolver.ResolveAsync(callback.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            await _client.AnswerCallbackQuery(callback.Id, "Not linked", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var restore = await _mediator.Send(
            new RestoreCardCommand(resolved.Value!.UserId, cardId), ct).ConfigureAwait(false);

        var text = restore.IsSuccess ? "✅ Card restored." : $"❌ {restore.Error!.Message}";
        await _client.EditMessageText(
            original.Chat.Id, original.MessageId, text,
            replyMarkup: null, cancellationToken: ct).ConfigureAwait(false);
        await _client.AnswerCallbackQuery(callback.Id, cancellationToken: ct).ConfigureAwait(false);
    }
}
