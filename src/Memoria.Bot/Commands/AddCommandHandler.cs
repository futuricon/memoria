using System.Globalization;

using MediatR;

using Memoria.Bot.Conversations;
using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Cards.Contracts.Commands;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Memoria.Bot.Commands;

internal sealed class AddCommandHandler : ITextCommandHandler
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;
    private readonly AddCardParser _parser;
    private readonly IConversationStateStore _conversations;

    public AddCommandHandler(
        ITelegramBotClient client,
        IMediator mediator,
        CurrentUserResolver resolver,
        AddCardParser parser,
        IConversationStateStore conversations)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(parser);
        ArgumentNullException.ThrowIfNull(conversations);
        _client = client;
        _mediator = mediator;
        _resolver = resolver;
        _parser = parser;
        _conversations = conversations;
    }

    public string CommandName => "add";

    public async Task HandleAsync(Message message, string? payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.From is null) return;

        if (string.IsNullOrWhiteSpace(payload))
        {
            _conversations.Start(message.Chat.Id,
                new AddCardDialogState(AddCardStep.WaitingForTitle));
            await _client.SendMessage(
                message.Chat.Id,
                "📝 Enter card title (or /cancel):",
                cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var resolved = await _resolver.ResolveAsync(message.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            await _client.SendMessage(
                message.Chat.Id, "❌ Not linked yet.", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var parsed = _parser.Parse(payload);
        if (parsed.IsFailure)
        {
            await _client.SendMessage(
                message.Chat.Id, $"❌ {parsed.Error!.Message}", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var pc = parsed.Value!;
        var result = await _mediator.Send(
            new AddCardCommand(resolved.Value!.UserId, pc.Title, pc.Body, pc.Tags), ct).ConfigureAwait(false);

        if (result.IsFailure)
        {
            await _client.SendMessage(
                message.Chat.Id, $"❌ {result.Error!.Message}", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var card = result.Value!;
        var idPrefix = card.Id.ToString("N", CultureInfo.InvariantCulture)[..8];
        await _client.SendMessage(
            message.Chat.Id,
            $"✅ Card `{idPrefix}` created.",
            parseMode: ParseMode.Markdown,
            cancellationToken: ct).ConfigureAwait(false);
    }
}
