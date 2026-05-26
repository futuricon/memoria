using System.Globalization;

using MediatR;

using Memoria.Bot.Conversations;
using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Cards.Contracts.Commands;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Memoria.Bot.Callbacks;

internal sealed class AddCardConfirmCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;
    private readonly IConversationStateStore _conversations;

    public AddCardConfirmCallbackHandler(
        ITelegramBotClient client,
        IMediator mediator,
        CurrentUserResolver resolver,
        IConversationStateStore conversations)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(conversations);
        _client = client;
        _mediator = mediator;
        _resolver = resolver;
        _conversations = conversations;
    }

    public string Prefix => "addcard:";

    public async Task HandleAsync(CallbackQuery callback, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (callback.Message is not { } original)
        {
            await _client.AnswerCallbackQuery(callback.Id, cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var data = callback.Data ?? string.Empty;

        if (string.Equals(data, "addcard:cancel", StringComparison.Ordinal))
        {
            _conversations.Clear(original.Chat.Id);
            await _client.EditMessageText(
                original.Chat.Id, original.MessageId, "❌ Cancelled.",
                replyMarkup: null, cancellationToken: ct).ConfigureAwait(false);
            await _client.AnswerCallbackQuery(callback.Id, cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        if (!string.Equals(data, "addcard:confirm", StringComparison.Ordinal))
        {
            await _client.AnswerCallbackQuery(callback.Id, "Bad callback", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        if (!_conversations.TryGet(original.Chat.Id, out var raw) || raw is not AddCardDialogState state
            || state.Step != AddCardStep.Preview
            || string.IsNullOrEmpty(state.Title)
            || string.IsNullOrEmpty(state.Body))
        {
            _conversations.Clear(original.Chat.Id);
            await _client.EditMessageText(
                original.Chat.Id, original.MessageId,
                "❌ Session expired. Use /add again.",
                replyMarkup: null, cancellationToken: ct).ConfigureAwait(false);
            await _client.AnswerCallbackQuery(callback.Id, cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var resolved = await _resolver.ResolveAsync(callback.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            _conversations.Clear(original.Chat.Id);
            await _client.EditMessageText(
                original.Chat.Id, original.MessageId, "❌ Not linked.",
                replyMarkup: null, cancellationToken: ct).ConfigureAwait(false);
            await _client.AnswerCallbackQuery(callback.Id, cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var tags = state.Tags ?? Array.Empty<string>();
        var add = await _mediator.Send(
            new AddCardCommand(resolved.Value!.UserId, state.Title!, state.Body!, tags), ct).ConfigureAwait(false);

        _conversations.Clear(original.Chat.Id);

        if (add.IsFailure)
        {
            await _client.EditMessageText(
                original.Chat.Id, original.MessageId,
                $"❌ {add.Error!.Message}",
                replyMarkup: null, cancellationToken: ct).ConfigureAwait(false);
        }
        else
        {
            var idPrefix = add.Value!.Id.ToString("N", CultureInfo.InvariantCulture)[..8];
            await _client.EditMessageText(
                original.Chat.Id, original.MessageId,
                $"✅ Card `{idPrefix}` created.",
                parseMode: ParseMode.Markdown,
                replyMarkup: null, cancellationToken: ct).ConfigureAwait(false);
        }

        await _client.AnswerCallbackQuery(callback.Id, cancellationToken: ct).ConfigureAwait(false);
    }
}
