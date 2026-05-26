using System.Globalization;

using Memoria.Bot.Commands;
using Memoria.Bot.Routing;
using Memoria.Bot.Services;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace Memoria.Bot.Callbacks;

internal sealed class ListPaginationCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient _client;
    private readonly ListCommandHandler _listCommand;
    private readonly CurrentUserResolver _resolver;

    public ListPaginationCallbackHandler(
        ITelegramBotClient client,
        ListCommandHandler listCommand,
        CurrentUserResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(listCommand);
        ArgumentNullException.ThrowIfNull(resolver);
        _client = client;
        _listCommand = listCommand;
        _resolver = resolver;
    }

    public string Prefix => "list:";

    public async Task HandleAsync(CallbackQuery callback, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var parts = (callback.Data ?? string.Empty).Split(':');
        if (parts.Length != 3
            || (parts[1] is not ("prev" or "next"))
            || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentPage))
        {
            await _client.AnswerCallbackQuery(callback.Id, "Bad callback", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var resolved = await _resolver.ResolveAsync(callback.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            await _client.AnswerCallbackQuery(callback.Id, "Not linked", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var targetPage = parts[1] == "next" ? currentPage + 1 : Math.Max(1, currentPage - 1);
        if (callback.Message is { } original)
        {
            await _listCommand.SendPageAsync(
                original.Chat.Id, resolved.Value!.UserId, targetPage, original.MessageId, ct).ConfigureAwait(false);
        }

        await _client.AnswerCallbackQuery(callback.Id, cancellationToken: ct).ConfigureAwait(false);
    }
}
