using System.Globalization;

using MediatR;

using Memoria.Bot.Routing;
using Memoria.Bot.Services;
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
    private readonly CurrentUserResolver _resolver;
    private readonly ILogger<StartCommandHandler> _logger;

    public StartCommandHandler(
        ITelegramBotClient client,
        IMediator mediator,
        CurrentUserResolver resolver,
        ILogger<StartCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _mediator = mediator;
        _resolver = resolver;
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

        await HandlePlainStartAsync(message, ct).ConfigureAwait(false);
    }

    private async Task HandlePlainStartAsync(Message message, CancellationToken ct)
    {
        if (message.From is null)
        {
            _logger.LogWarning("/start without From in chat {ChatId}", message.Chat.Id);
            return;
        }

        var resolved = await _resolver.ResolveAsync(message.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsSuccess)
        {
            await _client.SendMessage(
                message.Chat.Id,
                $"👋 Welcome back, {resolved.Value!.DisplayName}! Use /help to see available commands.",
                cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        // Auto-register: reuse the Telegram-widget command which atomically creates
        // a User + Telegram UserIdentity if the (Provider=Telegram, ExternalId) pair
        // has no match. The bot has no use for the JWT pair it returns — that's only
        // relevant for the SPA flow — but the user gets persisted, which is all we need.
        var displayName = string.IsNullOrWhiteSpace(message.From.LastName)
            ? message.From.FirstName
            : $"{message.From.FirstName} {message.From.LastName}";

        var registration = await _mediator.Send(new AuthenticateTelegramWidgetCommand(
            message.From.Id.ToString(CultureInfo.InvariantCulture),
            displayName), ct).ConfigureAwait(false);

        if (registration.IsFailure)
        {
            _logger.LogWarning(
                "Failed to auto-register Telegram user {TelegramId}: {Code} {Message}",
                message.From.Id, registration.Error!.Code, registration.Error.Message);
            await _client.SendMessage(
                message.Chat.Id,
                "⚠️ Could not create an account. Please try again later.",
                cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        await _client.SendMessage(
            message.Chat.Id,
            $"👋 Welcome to Memoria, {displayName}! Account created. Use /help to see commands.",
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
