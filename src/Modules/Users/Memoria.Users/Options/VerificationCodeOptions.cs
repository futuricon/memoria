using System.ComponentModel.DataAnnotations;

namespace Memoria.Users.Options;

internal sealed class VerificationCodeOptions
{
    public const string SectionName = "VerificationCode";

    [Range(1, 60)]
    public int TtlMinutesForLinking { get; init; } = 5;

    [Range(1, 60)]
    public int TtlMinutesForEmail { get; init; } = 10;

    [Range(1, 20)]
    public int MaxAttempts { get; init; } = 5;
}
