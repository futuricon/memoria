namespace Memoria.Reminders.Options;

internal sealed class RemindersOptions
{
    public const string SectionName = "Reminders";

    /// <summary>
    /// Ebbinghaus-curve interval ladder. Stage N (1-based) advances a card to
    /// <c>Intervals[N-1]</c> after the anchor. The number of stages equals
    /// <c>Intervals.Count</c>; <c>ReminderScheduler</c> only requires at least
    /// one entry. Always supplied from <c>appsettings.json:Reminders:Intervals</c>.
    /// <para>
    /// ⚠ <b>Do NOT add a default non-empty value here.</b> .NET's
    /// ConfigurationBinder appends config items onto existing collection
    /// elements rather than replacing them — a non-empty default plus the JSON
    /// entries produces a merged (too-long) list at runtime. Keep the default
    /// empty.
    /// </para>
    /// </summary>
    public IReadOnlyList<TimeSpan> Intervals { get; init; } = Array.Empty<TimeSpan>();

    /// <summary>
    /// Delay used to re-schedule a reminder at the SAME stage — after a
    /// <c>Hard</c> rating or a <c>Skip</c> the card does not advance, but the
    /// user is nudged again sooner than a full stage step. A scalar default is
    /// safe here: the ConfigurationBinder append bug only affects collections.
    /// </summary>
    public TimeSpan HardRetryInterval { get; init; } = TimeSpan.FromHours(1);
}
