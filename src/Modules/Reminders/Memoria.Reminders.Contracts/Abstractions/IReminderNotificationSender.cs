using Memoria.Shared.Kernel.Results;

namespace Memoria.Reminders.Contracts.Abstractions;

public interface IReminderNotificationSender
{
    /// <summary>
    /// Delivers the reminder to the user. Implementation chooses channel
    /// (Telegram on Stage 9). Returns the channel's message id on success
    /// (used later to edit the message in callback flows).
    /// Should NOT call MarkReminderSentCommand — the caller (SendReminderJob)
    /// does that.
    /// </summary>
    Task<Result<int>> SendReminderAsync(ReminderNotification notification, CancellationToken ct);
}