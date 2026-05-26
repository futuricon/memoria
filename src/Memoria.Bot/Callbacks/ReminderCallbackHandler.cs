using System.Globalization;

using MediatR;

using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Reminders.Contracts.Commands;
using Memoria.Reminders.Contracts.Queries;
using Memoria.Reviews.Contracts.Commands;
using Memoria.Reviews.Contracts.Dtos;

using Microsoft.Extensions.Logging;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Memoria.Bot.Callbacks;

/// <summary>
/// Маршрутизирует callback-ы вида <c>rem:&lt;reminderId&gt;:&lt;action&gt;[:&lt;param&gt;]</c>
/// на соответствующие mediator-команды. Поддерживаемые action-ы: <c>show</c>,
/// <c>skip</c>, <c>rate:{forgot|hard|good|easy}</c>.
/// </summary>
internal sealed class ReminderCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;
    private readonly ILogger<ReminderCallbackHandler> _logger;

    public ReminderCallbackHandler(
        ITelegramBotClient client,
        IMediator mediator,
        CurrentUserResolver resolver,
        ILogger<ReminderCallbackHandler> logger)
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

    public string Prefix => "rem:";

    public async Task HandleAsync(CallbackQuery callback, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(callback);

        var data = callback.Data ?? string.Empty;
        var parts = data.Split(':');
        if (parts.Length < 3)
        {
            await Answer(callback, "Bad callback", ct).ConfigureAwait(false);
            return;
        }

        if (!Guid.TryParseExact(parts[1], "N", out var reminderId)
            && !Guid.TryParse(parts[1], out reminderId))
        {
            await Answer(callback, "Bad reminder id", ct).ConfigureAwait(false);
            return;
        }

        var resolved = await _resolver.ResolveAsync(callback.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            await Answer(callback, "Not linked", ct).ConfigureAwait(false);
            return;
        }

        var userId = resolved.Value!.UserId;
        switch (parts[2])
        {
            case "show":
                await HandleShowAsync(callback, reminderId, userId, ct).ConfigureAwait(false);
                break;
            case "skip":
                await HandleSkipAsync(callback, reminderId, userId, ct).ConfigureAwait(false);
                break;
            case "rate" when parts.Length == 4:
                await HandleRateAsync(callback, reminderId, userId, parts[3], ct).ConfigureAwait(false);
                break;
            default:
                await Answer(callback, "Unknown action", ct).ConfigureAwait(false);
                break;
        }
    }

    private async Task HandleShowAsync(CallbackQuery callback, Guid reminderId, Guid userId, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new RevealReminderAnswerCommand(reminderId, userId), ct).ConfigureAwait(false);

        if (result.IsFailure)
        {
            await Answer(callback, result.Error!.Message, ct).ConfigureAwait(false);
            return;
        }

        var answer = result.Value!;
        var idN = reminderId.ToString("N", CultureInfo.InvariantCulture);
        var text =
            $"🔔 *{Escape(answer.Title)}*\n\n" +
            "📖 Answer:\n" +
            $"{Escape(answer.Body)}\n\n" +
            "How well did you remember?";

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("😕 Forgot", $"rem:{idN}:rate:forgot"),
                InlineKeyboardButton.WithCallbackData("🤔 Hard", $"rem:{idN}:rate:hard"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🙂 Good", $"rem:{idN}:rate:good"),
                InlineKeyboardButton.WithCallbackData("😎 Easy", $"rem:{idN}:rate:easy"),
            },
        });

        await EditAsync(callback, text, keyboard, ct).ConfigureAwait(false);
        await Answer(callback, null, ct).ConfigureAwait(false);
    }

    private async Task HandleSkipAsync(CallbackQuery callback, Guid reminderId, Guid userId, CancellationToken ct)
    {
        var result = await _mediator.Send(new SkipReminderCommand(reminderId, userId), ct).ConfigureAwait(false);
        var text = result.IsSuccess ? "⏭ Skipped." : $"❌ {result.Error!.Message}";
        await EditAsync(callback, text, replyMarkup: null, ct).ConfigureAwait(false);
        await Answer(callback, null, ct).ConfigureAwait(false);
    }

    private async Task HandleRateAsync(CallbackQuery callback, Guid reminderId, Guid userId, string ratingToken, CancellationToken ct)
    {
        if (!TryParseRating(ratingToken, out var rating))
        {
            await Answer(callback, "Bad rating", ct).ConfigureAwait(false);
            return;
        }

        var reminder = await _mediator.Send(new GetReminderQuery(reminderId, userId), ct).ConfigureAwait(false);
        if (reminder.IsFailure)
        {
            await Answer(callback, reminder.Error!.Message, ct).ConfigureAwait(false);
            return;
        }

        var review = await _mediator.Send(
            new RecordReviewCommand(userId, reminder.Value!.CardId, reminderId, rating, Note: null),
            ct).ConfigureAwait(false);

        if (review.IsFailure)
        {
            await Answer(callback, review.Error!.Message, ct).ConfigureAwait(false);
            return;
        }

        var emoji = rating switch
        {
            Rating.Forgot => "😕",
            Rating.Hard => "🤔",
            Rating.Good => "🙂",
            Rating.Easy => "😎",
            _ => "✅",
        };

        await EditAsync(
            callback,
            $"✅ {Escape(review.Value!.CardTitleSnapshot)} — rated {emoji}",
            replyMarkup: null, ct).ConfigureAwait(false);
        await Answer(callback, null, ct).ConfigureAwait(false);
    }

    private static bool TryParseRating(string token, out Rating rating)
    {
        switch (token)
        {
            case "forgot": rating = Rating.Forgot; return true;
            case "hard": rating = Rating.Hard; return true;
            case "good": rating = Rating.Good; return true;
            case "easy": rating = Rating.Easy; return true;
            default: rating = default; return false;
        }
    }

    private async Task EditAsync(CallbackQuery callback, string text, InlineKeyboardMarkup? replyMarkup, CancellationToken ct)
    {
        if (callback.Message is not { } original)
        {
            return;
        }

        try
        {
            await _client.EditMessageText(
                original.Chat.Id, original.MessageId, text,
                parseMode: ParseMode.Markdown,
                replyMarkup: replyMarkup, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to edit reminder message {MessageId}", original.MessageId);
        }
    }

    private async Task Answer(CallbackQuery callback, string? text, CancellationToken ct)
    {
        try
        {
            await _client.AnswerCallbackQuery(callback.Id, text, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to answer callback {CallbackId}", callback.Id);
        }
    }

    private static string Escape(string s) =>
        s
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);
}
