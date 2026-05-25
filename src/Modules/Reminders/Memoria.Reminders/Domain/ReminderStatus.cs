namespace Memoria.Reminders.Domain;

internal enum ReminderStatus
{
    Pending,
    Sending,
    Sent,
    Confirmed,
    Skipped,
    Failed,
    Cancelled,
}