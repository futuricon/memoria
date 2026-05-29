using System.Globalization;

using MediatR;

using Memoria.Bot.Commands;
using Memoria.Bot.Localization;
using Memoria.Bot.Routing;
using Memoria.Bot.Services;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;

using Microsoft.Extensions.Logging;

using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Memoria.Bot.Callbacks;

/// <summary>
/// Handles the inline menu (prefix <c>menu:</c>). Navigation edits the menu
/// message in place; action buttons (add/list/tags/help/login/timezone) reuse
/// the corresponding text-command handlers via a synthetic <see cref="Message"/>
/// so there's no duplicated rendering. Settings → quiet-hours are set with
/// presets straight from here.
/// </summary>
internal sealed class MenuCallbackHandler : ICallbackHandler
{
    private readonly ITelegramBotClient _client;
    private readonly IMediator _mediator;
    private readonly CurrentUserResolver _resolver;
    private readonly Dictionary<string, ITextCommandHandler> _commands;
    private readonly ILogger<MenuCallbackHandler> _logger;

    public MenuCallbackHandler(
        ITelegramBotClient client,
        IMediator mediator,
        CurrentUserResolver resolver,
        IEnumerable<ITextCommandHandler> commands,
        ILogger<MenuCallbackHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _mediator = mediator;
        _resolver = resolver;
        _commands = commands.ToDictionary(h => h.CommandName, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public string Prefix => "menu:";

    public async Task HandleAsync(CallbackQuery callback, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (callback.Message is not { } origin)
        {
            await Answer(callback, null, ct).ConfigureAwait(false);
            return;
        }

        var parts = (callback.Data ?? string.Empty).Split(':');
        var action = parts.Length > 1 ? parts[1] : "home";

        switch (action)
        {
            case "home":
                await EditTo(origin, BotText.MenuTitle, MenuCommandHandler.HomeKeyboard, ct).ConfigureAwait(false);
                await Answer(callback, null, ct).ConfigureAwait(false);
                break;
            case "settings":
                await ShowSettingsAsync(callback, origin, ct).ConfigureAwait(false);
                break;
            case "quiet":
                await ShowQuietAsync(callback, origin, ct).ConfigureAwait(false);
                break;
            case "quietset":
                await SetQuietAsync(callback, origin, parts.Length > 2 ? parts[2] : string.Empty, ct).ConfigureAwait(false);
                break;
            case "add":
            case "list":
            case "due":
            case "tags":
            case "help":
            case "login":
            case "tz":
                await DelegateToCommandAsync(callback, origin, action, ct).ConfigureAwait(false);
                break;
            default:
                await Answer(callback, "Unknown action", ct).ConfigureAwait(false);
                break;
        }
    }

    private async Task DelegateToCommandAsync(CallbackQuery callback, Message origin, string action, CancellationToken ct)
    {
        await Answer(callback, null, ct).ConfigureAwait(false); // dismiss the spinner first

        var commandName = action == "tz" ? "timezone" : action;
        if (!_commands.TryGetValue(commandName, out var handler))
        {
            _logger.LogWarning("Menu action '{Action}' has no command handler '{Command}'", action, commandName);
            return;
        }

        // Synthetic message: the command handlers only read From.Id + Chat.Id.
        var synthetic = new Message { Chat = origin.Chat, From = callback.From };
        await handler.HandleAsync(synthetic, payload: null, ct).ConfigureAwait(false);
    }

    private async Task ShowSettingsAsync(CallbackQuery callback, Message origin, CancellationToken ct)
    {
        var prefs = await LoadPrefsAsync(callback, ct).ConfigureAwait(false);
        if (prefs is null)
        {
            return;
        }

        await EditTo(origin, SettingsText(prefs.TimeZoneId, prefs.QuietHoursStart, prefs.QuietHoursEnd), SettingsKeyboard, ct)
            .ConfigureAwait(false);
        await Answer(callback, null, ct).ConfigureAwait(false);
    }

    private async Task ShowQuietAsync(CallbackQuery callback, Message origin, CancellationToken ct)
    {
        var prefs = await LoadPrefsAsync(callback, ct).ConfigureAwait(false);
        if (prefs is null)
        {
            return;
        }

        var text = BotText.QuietTitle + "\n\nCurrent: " + FormatQuiet(prefs.QuietHoursStart, prefs.QuietHoursEnd);
        await EditTo(origin, text, QuietKeyboard, ct).ConfigureAwait(false);
        await Answer(callback, null, ct).ConfigureAwait(false);
    }

    private async Task SetQuietAsync(CallbackQuery callback, Message origin, string token, CancellationToken ct)
    {
        if (!TryParseQuiet(token, out var start, out var end))
        {
            await Answer(callback, "Bad value", ct).ConfigureAwait(false);
            return;
        }

        var prefs = await LoadPrefsAsync(callback, ct).ConfigureAwait(false);
        if (prefs is null)
        {
            return;
        }

        var update = await _mediator.Send(
            new UpdateUserPreferencesCommand(prefs.UserId, prefs.TimeZoneId, start, end), ct).ConfigureAwait(false);
        if (update.IsFailure)
        {
            await Answer(callback, BotText.SomethingWrong, ct).ConfigureAwait(false);
            return;
        }

        await EditTo(origin, SettingsText(prefs.TimeZoneId, start, end), SettingsKeyboard, ct).ConfigureAwait(false);
        await Answer(callback, BotText.Saved, ct).ConfigureAwait(false);
    }

    private async Task<UserPreferencesDto?> LoadPrefsAsync(CallbackQuery callback, CancellationToken ct)
    {
        var resolved = await _resolver.ResolveAsync(callback.From.Id, ct).ConfigureAwait(false);
        if (resolved.IsFailure)
        {
            await Answer(callback, "Not linked", ct).ConfigureAwait(false);
            return null;
        }

        var prefs = await _mediator.Send(new GetUserPreferencesQuery(resolved.Value!.UserId), ct).ConfigureAwait(false);
        if (prefs.IsFailure)
        {
            await Answer(callback, BotText.SomethingWrong, ct).ConfigureAwait(false);
            return null;
        }

        return prefs.Value;
    }

    private static string SettingsText(string timeZoneId, TimeOnly? quietStart, TimeOnly? quietEnd) =>
        BotText.SettingsTitle + "\n\n" +
        $"🕘 {BotText.TimezoneLabel}: {Escape(timeZoneId)}\n" +
        $"🌙 {BotText.QuietHoursLabel}: {FormatQuiet(quietStart, quietEnd)}";

    private static string FormatQuiet(TimeOnly? start, TimeOnly? end) =>
        start is { } s && end is { } e
            ? $"{s.ToString("HH:mm", CultureInfo.InvariantCulture)}–{e.ToString("HH:mm", CultureInfo.InvariantCulture)}"
            : BotText.QuietHoursOff;

    private static bool TryParseQuiet(string token, out TimeOnly? start, out TimeOnly? end)
    {
        start = null;
        end = null;

        if (string.Equals(token, "off", StringComparison.Ordinal))
        {
            return true;
        }

        var halves = token.Split('-');
        if (halves.Length != 2 || !TryHHmm(halves[0], out var s) || !TryHHmm(halves[1], out var e))
        {
            return false;
        }

        start = s;
        end = e;
        return true;
    }

    private static bool TryHHmm(string value, out TimeOnly time)
    {
        time = default;
        if (value.Length != 4
            || !int.TryParse(value.AsSpan(0, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var h)
            || !int.TryParse(value.AsSpan(2, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var m)
            || h is < 0 or > 23
            || m is < 0 or > 59)
        {
            return false;
        }

        time = new TimeOnly(h, m);
        return true;
    }

    private async Task EditTo(Message origin, string text, InlineKeyboardMarkup keyboard, CancellationToken ct)
    {
        try
        {
            await _client.EditMessageText(
                origin.Chat.Id, origin.MessageId, text,
                parseMode: ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct).ConfigureAwait(false);
        }
        catch (ApiRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to edit menu message {MessageId}", origin.MessageId);
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

    private static InlineKeyboardMarkup SettingsKeyboard { get; } = new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData(BotText.BtnChangeTimezone, "menu:tz") },
        new[] { InlineKeyboardButton.WithCallbackData(BotText.BtnQuietHours, "menu:quiet") },
        new[] { InlineKeyboardButton.WithCallbackData(BotText.BtnLogin, "menu:login") },
        new[] { InlineKeyboardButton.WithCallbackData(BotText.BtnBack, "menu:home") },
    });

    private static InlineKeyboardMarkup QuietKeyboard { get; } = new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("22:00–08:00", "menu:quietset:2200-0800"),
            InlineKeyboardButton.WithCallbackData("23:00–07:00", "menu:quietset:2300-0700"),
        },
        new[]
        {
            InlineKeyboardButton.WithCallbackData("00:00–07:00", "menu:quietset:0000-0700"),
            InlineKeyboardButton.WithCallbackData(BotText.BtnQuietOff, "menu:quietset:off"),
        },
        new[] { InlineKeyboardButton.WithCallbackData(BotText.BtnBack, "menu:settings") },
    });
}
