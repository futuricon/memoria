namespace Memoria.Reminders.Contracts.Dtos;

/// <summary>
/// Reminder engagement counts in a trailing window: how many reminders
/// were sent, how many were confirmed (user revealed + rated), how many
/// were skipped, how many failed. Drives the dashboard "are reminders
/// landing?" KPI.
/// </summary>
public sealed record ReminderSkipRateDto(
    int Sent,
    int Confirmed,
    int Skipped,
    int Failed)
{
    public int Total => Sent + Confirmed + Skipped + Failed;
}
