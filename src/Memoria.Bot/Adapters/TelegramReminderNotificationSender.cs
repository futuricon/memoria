using System.Globalization;

using MediatR;

using Memoria.Bot.Conversations;
using Memoria.Cards.Contracts.Dtos;
using Memoria.Reminders.Contracts.Abstractions;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Queries;

using Microsoft.Extensions.Logging;

using Polly;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Memoria.Bot.Adapters;

internal sealed class TelegramReminderNotificationSender : IReminderNotificationSender
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly IConversationStateStore _conversations;
    private readonly ILogger<TelegramReminderNotificationSender> _logger;
    private readonly IAsyncPolicy _retryPolicy;

    public TelegramReminderNotificationSender(
        ITelegramBotClient client,
        IMediator mediator,
        IConversationStateStore conversations,
        ILogger<TelegramReminderNotificationSender> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(conversations);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _mediator = mediator;
        _conversations = conversations;
        _logger = logger;
        _retryPolicy = TelegramRateLimitPolicy.Build(logger);
    }

    public async Task<Result<int>> SendReminderAsync(ReminderNotification notification, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var idsResult = await _mediator.Send(
            new GetUserIdentitiesQuery(notification.UserId), ct).ConfigureAwait(false);
        if (idsResult.IsFailure)
        {
            return Result<int>.Failure(idsResult.Error!);
        }

        var telegramId = idsResult.Value!
            .FirstOrDefault(i => string.Equals(i.Provider, "Telegram", StringComparison.Ordinal))
            ?.ExternalId;
        if (telegramId is null)
        {
            return Result<int>.Failure(Error.NotFound(
                "bot.no_telegram_identity",
                "User has no linked Telegram account."));
        }

        if (!long.TryParse(telegramId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chatId))
        {
            return Result<int>.Failure(Error.Unexpected(
                "bot.bad_telegram_id",
                "Stored Telegram id is not a number."));
        }

        var idN = notification.ReminderId.ToString("N", CultureInfo.InvariantCulture);
        var (text, keyboard) = notification.CardType == CardType.Question
            ? BuildQuestionPrompt(notification, idN)
            : BuildNotePrompt(notification, idN);

        try
        {
            var sent = await _retryPolicy.ExecuteAsync(
                token => _client.SendMessage(
                    chatId, text, parseMode: ParseMode.Markdown,
                    replyMarkup: keyboard, cancellationToken: token),
                ct).ConfigureAwait(false);

            if (notification.CardType == CardType.Question)
            {
                ArmAwaitingAnswer(chatId, notification);
            }

            return Result<int>.Success(sent.MessageId);
        }
        catch (ApiRequestException ex) when (ex.ErrorCode == 429)
        {
            _logger.LogWarning(ex,
                "Telegram 429 (after retries) on reminder {ReminderId}", notification.ReminderId);
            return Result<int>.Failure(Error.Unexpected(
                "bot.telegram_rate_limit", "Telegram rate limit, retry later."));
        }
        catch (ApiRequestException ex)
        {
            _logger.LogError(ex, "Telegram API error sending reminder {ReminderId}", notification.ReminderId);
            return Result<int>.Failure(Error.Unexpected("bot.telegram_send_failed", ex.Message));
        }
    }

    private static (string Text, InlineKeyboardMarkup Keyboard) BuildNotePrompt(
        ReminderNotification notification, string idN)
    {
        var tagsLine = TagsLine(notification.Tags);
        var text =
            "🔔 Time to review!\n" +
            $"*{Escape(notification.CardTitle)}*\n" +
            (tagsLine.Length > 0 ? tagsLine + "\n" : string.Empty) +
            "\nRecall the answer, then tap a button.";

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("📖 Show answer", $"rem:{idN}:show"),
                InlineKeyboardButton.WithCallbackData("⏭ Skip", $"rem:{idN}:skip"),
            },
        });

        return (text, keyboard);
    }

    private static (string Text, InlineKeyboardMarkup Keyboard) BuildQuestionPrompt(
        ReminderNotification notification, string idN)
    {
        var tagsLine = TagsLine(notification.Tags);
        var text =
            "🔔 Time to review!\n" +
            $"*{Escape(notification.CardTitle)}*\n" +
            (tagsLine.Length > 0 ? tagsLine + "\n" : string.Empty) +
            "\n✍️ Reply with your answer — I'll grade it.";

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("⏭ Skip", $"rem:{idN}:skip"),
            },
        });

        return (text, keyboard);
    }

    private void ArmAwaitingAnswer(long chatId, ReminderNotification notification)
    {
        // Don't clobber an in-progress /add dialog; that flow takes precedence.
        if (_conversations.TryGet(chatId, out var existing) && existing is AddCardDialogState)
        {
            return;
        }

        _conversations.Start(chatId, new AwaitingAnswerState(notification.ReminderId, notification.CardId));
    }

    private static string TagsLine(IReadOnlyList<string> tags) =>
        tags.Count > 0
            ? "🏷 " + string.Join(" ", tags.Select(t => "#" + t))
            : string.Empty;

    private static string Escape(string s) =>
        s
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);
}
