using System.ComponentModel.DataAnnotations;

namespace Memoria.Api.Configuration;

/// <summary>
/// Allowlist email-ов, которым разрешён доступ к <c>/jobs</c> Hangfire-dashboard.
/// Реальные значения держим в user-secrets / переменных окружения
/// (<c>Hangfire__AllowedEmails__0</c>), но <c>appsettings.json</c> должен содержать
/// хотя бы пустой массив, иначе биндинг падает на ValidateOnStart.
/// </summary>
public sealed class HangfireDashboardOptions
{
    public const string SectionName = "Hangfire";

    [Required]
    public IReadOnlyList<string> AllowedEmails { get; init; } = Array.Empty<string>();
}
