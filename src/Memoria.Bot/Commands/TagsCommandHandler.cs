using MediatR;

using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Cards.Contracts.Queries;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace Memoria.Bot.Commands;

internal sealed class TagsCommandHandler : ITextCommandHandler
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;

    public TagsCommandHandler(
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

    public string CommandName => "tags";

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

        var tagsResult = await _mediator.Send(
            new GetTagsQuery(resolved.Value!.UserId), ct).ConfigureAwait(false);

        if (tagsResult.IsFailure)
        {
            await _client.SendMessage(
                message.Chat.Id,
                $"❌ {tagsResult.Error!.Message}",
                cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var tags = tagsResult.Value!;
        var reply = tags.Count == 0
            ? "🏷 You have no tags yet."
            : "🏷 Your tags:\n\n" + string.Join("\n", tags.Select(t => $"#{t.Name} ({t.CardCount})"));

        await _client.SendMessage(message.Chat.Id, reply, cancellationToken: ct).ConfigureAwait(false);
    }
}
