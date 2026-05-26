using System.Globalization;

using MediatR;

using Memoria.Bot.Routing;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;

using Microsoft.Extensions.Logging;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace Memoria.Bot.Commands;

internal sealed class StartCommandHandler : ITextCommandHandler
{
    private const string LinkPrefix = "link_";

    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly ILogger<StartCommandHandler> _logger;

    public StartCommandHandler(
        ITelegramBotClient client,
        IMediator mediator,
        ILogger<StartCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _mediator = mediator;
        _logger = logger;
    }

    public string CommandName => "start";

    public async Task HandleAsync(Message message, string? payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (payload is { Length: > 5 } && payload.StartsWith(LinkPrefix, StringComparison.Ordinal))
        {
            await HandleDeepLinkAsync(message, payload[LinkPrefix.Length..], ct).ConfigureAwait(false);
            return;
        }

        await _client.SendMessage(
            message.Chat.Id,
            "👋 Welcome to Memoria! Use /help to see available commands.",
            cancellationToken: ct).ConfigureAwait(false);
    }

    private async Task HandleDeepLinkAsync(Message message, string token, CancellationToken ct)
    {
        if (message.From is null)
        {
            _logger.LogWarning("Deep-link /start without From in chat {ChatId}", message.Chat.Id);
            return;
        }

        var telegramId = message.From.Id.ToString(CultureInfo.InvariantCulture);
        var result = await _mediator.Send(
            new CompleteTelegramLinkingCommand(token, telegramId), ct).ConfigureAwait(false);

        var reply = result.IsSuccess
            ? "✅ Telegram account linked successfully."
            : result.Error!.Type switch
            {
                ErrorType.NotFound => "❌ Unknown or expired linking token.",
                ErrorType.Validation => "❌ Linking token has expired. Please request a new one in the app.",
                ErrorType.Conflict => "❌ This Telegram is already linked to another account.",
                _ => "❌ Could not link account, please try again.",
            };

        await _client.SendMessage(message.Chat.Id, reply, cancellationToken: ct).ConfigureAwait(false);
    }
}
