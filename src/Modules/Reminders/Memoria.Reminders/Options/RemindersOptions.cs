using System.ComponentModel.DataAnnotations;

namespace Memoria.Reminders.Options;

internal sealed class RemindersOptions
{
    public const string SectionName = "Reminders";

    public IReadOnlyList<TimeSpan> Intervals { get; init; } = new[]
    {
        TimeSpan.Zero,
        TimeSpan.FromMinutes(25),
        TimeSpan.FromDays(1),
        TimeSpan.FromDays(18),
        TimeSpan.FromDays(75),
    };

    [Range(1, 365)]
    public int SoftDeleteRetentionDays { get; init; } = 90;

    [Required]
    public string PurgeCronExpression { get; init; } = "0 4 * * *";
}