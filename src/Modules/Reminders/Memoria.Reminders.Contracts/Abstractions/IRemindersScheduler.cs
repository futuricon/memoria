namespace Memoria.Reminders.Contracts.Abstractions;

public interface IRemindersScheduler
{
    /// <summary>
    /// Cancels all pending reminders for a card and removes corresponding
    /// Hangfire jobs. Idempotent — safe to call when nothing is scheduled.
    /// </summary>
    Task CancelForCardAsync(Guid cardId, CancellationToken ct);
}