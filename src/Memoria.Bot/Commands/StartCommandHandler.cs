using System.Globalization;

using MediatR;

using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Shared.Infrastructure.Options;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Contracts.Dtos;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Telegram.Bot;
using Telegram.Bot.Types;

namespace Memoria.Bot.Commands;

internal sealed class StartCommandHandler : ITextCommandHandler
{
    private const string LinkPrefix = "link_";

    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;
    private readonly TelegramOptions _telegram;
    private readonly ILogger<StartCommandHandler> _logger;

    public StartCommandHandler(
        ITelegramBotClient client,
        IMediator mediator,
        CurrentUserResolver resolver,
        IOptions<TelegramOptions> telegram,
        ILogger<StartCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(telegram);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _mediator = mediator;
        _resolver = resolver;
        _telegram = telegram.Value;
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
                $"👋 Welcome back, {resolved.Value!.DisplayName}! Pick an action below or use /menu anytime.",
                replyMarkup: MenuCommandHandler.HomeKeyboard,
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
            displayName,
            message.From.Username), ct).ConfigureAwait(false);

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
            $"👋 Welcome to Memoria, {displayName}! Account created. Pick an action below or use /menu anytime.",
            replyMarkup: MenuCommandHandler.HomeKeyboard,
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
            new CompleteTelegramLinkingCommand(token, telegramId, message.From.Username), ct).ConfigureAwait(false);

        var reply = result.IsSuccess
            ? FormatLinkingSuccess(result.Value!)
            : result.Error!.Type switch
            {
                ErrorType.NotFound => "❌ Unknown or expired linking token.",
                ErrorType.Validation => "❌ Linking token has expired. Please request a new one in the app.",
                ErrorType.Conflict => "❌ This Telegram is linked to a different account that can't be merged automatically. Contact support.",
                _ => "❌ Could not link account, please try again.",
            };

        await _client.SendMessage(message.Chat.Id, reply, cancellationToken: ct).ConfigureAwait(false);
    }

    private string FormatLinkingSuccess(TelegramLinkingResultDto result)
    {
        if (!result.Merged || result.MergeStats is null)
        {
            return "✅ Telegram account linked successfully.";
        }

        // Use the largest non-zero count so the message reads naturally even
        // if the source had reviews but no cards (or vice versa).
        var stats = result.MergeStats;
        var headline = Math.Max(stats.CardsMoved, Math.Max(stats.RemindersMoved, stats.ReviewsMoved));

        if (headline == 0)
        {
            // Edge case: merge ran but source was already empty. No value in
            // mentioning the merge — just confirm the link.
            return "✅ Telegram account linked successfully.";
        }

        var noun = stats.CardsMoved > 0
            ? Plural(stats.CardsMoved, "card", "cards")
            : Plural(headline, "item", "items");

        var body = $"✅ Telegram linked. Merged {stats.CardsMoved} {noun} from your old Telegram-only account.";

        if (!string.IsNullOrWhiteSpace(_telegram.SpaUrl))
        {
            body += $" View them at {_telegram.SpaUrl.TrimEnd('/')}/cards";
        }

        return body;
    }

    private static string Plural(int n, string one, string many) => n == 1 ? one : many;
}
