using System.Globalization;

using MediatR;

using Memoria.Bot.Localization;
using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Reminders.Contracts.Queries;
using Memoria.Users.Contracts.Queries;

using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Memoria.Bot.Commands;

/// <summary>
/// <c>/due</c> — lists the cards due for review today (in the user's timezone)
/// and lets them review one right now: tapping a card enqueues immediate
/// delivery of its reminder, which arrives with the normal review UI.
/// </summary>
internal sealed class DueCommandHandler : ITextCommandHandler
{
    private const int MaxButtons = 10;
    private const int MaxTitleOnButton = 32;

    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;
    private readonly TimeProvider _clock;

    public DueCommandHandler(
        ITelegramBotClient client,
        IMediator mediator,
        CurrentUserResolver resolver,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(clock);
        _client = client;
        _mediator = mediator;
        _resolver = resolver;
        _clock = clock;
    }

    public string CommandName => "due";

    public async Task HandleAsync(Message message, string? payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.From is null) return;

        var resolved = await _resolver.ResolveAsync(message.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            await _client.SendMessage(message.Chat.Id, BotText.NotLinked, cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var userId = resolved.Value!.UserId;
        var today = await ResolveTodayAsync(userId, ct).ConfigureAwait(false);

        var dueResult = await _mediator.Send(new GetDueRemindersForUserQuery(userId, today), ct).ConfigureAwait(false);
        if (dueResult.IsFailure)
        {
            await _client.SendMessage(
                message.Chat.Id, $"❌ {dueResult.Error!.Message}", cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var due = dueResult.Value!;
        if (due.Count == 0)
        {
            await _client.SendMessage(message.Chat.Id, BotText.DueNothing, cancellationToken: ct).ConfigureAwait(false);
            return;
        }

        var shown = due.Take(MaxButtons).ToList();
        var text = $"⏰ *Due now ({due.Count.ToString(CultureInfo.InvariantCulture)})*\n\n" +
                   string.Join("\n", shown.Select(d => "• " + Escape(d.CardTitle)));

        var rows = shown.Select(d => new[]
        {
            InlineKeyboardButton.WithCallbackData(
                "▶️ " + Truncate(d.CardTitle),
                "due:rev:" + d.ReminderId.ToString("N", CultureInfo.InvariantCulture)),
        });

        await _client.SendMessage(
            message.Chat.Id, text, parseMode: ParseMode.Markdown,
            replyMarkup: new InlineKeyboardMarkup(rows), cancellationToken: ct).ConfigureAwait(false);
    }

    private async Task<DateOnly> ResolveTodayAsync(Guid userId, CancellationToken ct)
    {
        var prefs = await _mediator.Send(new GetUserPreferencesQuery(userId), ct).ConfigureAwait(false);
        var timeZone = TimeZoneInfo.Utc;
        if (prefs.IsSuccess)
        {
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(prefs.Value!.TimeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                // fall back to UTC
            }
            catch (InvalidTimeZoneException)
            {
                // fall back to UTC
            }
        }

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(_clock.GetUtcNow().UtcDateTime, timeZone);
        return DateOnly.FromDateTime(localNow);
    }

    private static string Truncate(string s) =>
        s.Length <= MaxTitleOnButton ? s : s[..(MaxTitleOnButton - 1)] + "…";

    private static string Escape(string s) =>
        s
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);
}
