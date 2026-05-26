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
}
