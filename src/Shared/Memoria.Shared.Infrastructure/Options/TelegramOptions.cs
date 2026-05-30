using System.ComponentModel.DataAnnotations;

namespace Memoria.Shared.Infrastructure.Options;

/// <summary>
/// Конфигурация Telegram-бота. Используется сразу несколькими модулями:
/// Memoria.Users формирует deep-link при привязке аккаунта,
/// Memoria.Bot поднимает long polling и адаптер
/// <c>TelegramReminderNotificationSender</c>.
/// </summary>
public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    [Required]
    public string BotToken { get; init; } = string.Empty;

    [Required]
    public string BotUsername { get; init; } = string.Empty;

    /// <summary>
    /// Public URL of the SPA, embedded into bot replies that want to point
    /// the user back to the web app (e.g. "Merged N cards. View them at &lt;SpaUrl&gt;/cards").
    /// Optional — when empty the bot omits the link from its reply.
    /// </summary>
    public string? SpaUrl { get; init; }
}
