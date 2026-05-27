namespace Memoria.Reminders.Options;

internal sealed class RemindersOptions
{
    public const string SectionName = "Reminders";

    /// <summary>
    /// Ebbinghaus-curve intervals between successive reminders. Always supplied
    /// from <c>appsettings.json:Reminders:Intervals</c>; <c>ReminderScheduler</c>
    /// validates that there are exactly the expected number.
    /// <para>
    /// ⚠ <b>Do NOT add a default non-empty value here.</b> .NET's
    /// ConfigurationBinder appends config items onto existing collection
    /// elements rather than replacing them — a default of 5 values + 5 from
    /// JSON produces 10 entries at runtime. Keep the default empty.
    /// </para>
    /// </summary>
    public IReadOnlyList<TimeSpan> Intervals { get; init; } = Array.Empty<TimeSpan>();
}
