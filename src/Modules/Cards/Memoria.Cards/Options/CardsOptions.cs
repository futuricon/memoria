using System.ComponentModel.DataAnnotations;

namespace Memoria.Cards.Options;

public sealed class CardsOptions
{
    public const string SectionName = "Cards";

    [Range(1, 3650)]
    public int SoftDeleteRetentionDays { get; init; } = 90;

    [Required]
    public string PurgeCronExpression { get; init; } = "0 4 * * *";
}
