namespace Memoria.Reminders.Domain;

public enum ReminderStatus
{
    Pending,
    Sending,
    Sent,
    Confirmed,
    Skipped,
    Failed,
    Cancelled,
}