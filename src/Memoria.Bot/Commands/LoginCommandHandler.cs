using MediatR;

using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Users.Contracts.Commands;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Memoria.Bot.Commands;

internal sealed class LoginCommandHandler : ITextCommandHandler
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;

    public LoginCommandHandler(
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

    public string CommandName => "login";

    public async Task HandleAsync(Message message, string? payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.From is null) return;

        var resolved = await _resolver.ResolveAsync(message.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            await _client.SendMessage(
                message.Chat.Id,
                "❌ Not linked yet. Use the bot from your SPA registration flow.",
                cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var code = await _mediator.Send(
            new GenerateBotLoginCodeCommand(resolved.Value!.UserId), ct).ConfigureAwait(false);

        if (code.IsFailure)
        {
            await _client.SendMessage(
                message.Chat.Id,
                "❌ Could not generate code. Please try again later.",
                cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var reply =
            "🔐 Your one-time login code:\n\n" +
            "`" + code.Value + "`\n\n" +
            "Valid for 5 minutes. Paste it in the SPA login form.";

        await _client.SendMessage(
            message.Chat.Id, reply, parseMode: ParseMode.Markdown, cancellationToken: ct).ConfigureAwait(false);
    }
}
