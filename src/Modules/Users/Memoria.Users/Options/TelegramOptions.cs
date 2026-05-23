using System.ComponentModel.DataAnnotations;

namespace Memoria.Users.Options;

internal sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    [Required]
    public string BotToken { get; init; } = string.Empty;

    [Required]
    public string BotUsername { get; init; } = string.Empty;
}
