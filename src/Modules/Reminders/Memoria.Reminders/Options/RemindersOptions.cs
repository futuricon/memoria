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
}
