namespace Memoria.Reminders.Domain;

public sealed class Reminder
{
    public Guid Id { get; private set; }
    public Guid CardId { get; private set; }
    public Guid UserId { get; private set; }
    public int StageNumber { get; private set; }
    public DateTime ScheduledAt { get; private set; }
    public ReminderStatus Status { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public string? HangfireJobId { get; private set; }
    public int? MessageId { get; private set; }

    private Reminder()
    {
        /* EF Core */
    }

    public Reminder(Guid cardId, Guid userId, int stageNumber, DateTime scheduledAt)
    {
        Id = Guid.NewGuid();
        CardId = cardId;
        UserId = userId;
        StageNumber = stageNumber;
        ScheduledAt = scheduledAt;
        Status = ReminderStatus.Pending;
    }

    public void AttachHangfireJob(string hangfireJobId)
    {
        // Allowed in any status (Hangfire id может быть назначен/перезаписан).
        HangfireJobId = hangfireJobId;
    }

    public void BeginSending()
    {
        if (Status is not ReminderStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot BeginSending reminder in status {Status}");
        }

        Status = ReminderStatus.Sending;
    }

    public void MarkSent(int messageId, DateTime utcNow)
    {
        if (Status is not ReminderStatus.Sending)
        {
            throw new InvalidOperationException($"Cannot MarkSent reminder in status {Status}");
        }

        Status = ReminderStatus.Sent;
        SentAt = utcNow;
        MessageId = messageId;
    }

    public void Confirm(DateTime utcNow)
    {
        if (Status is not ReminderStatus.Sent)
        {
            throw new InvalidOperationException($"Cannot Confirm reminder in status {Status}");
        }

        Status = ReminderStatus.Confirmed;
        ConfirmedAt = utcNow;
    }

    public void Skip(DateTime utcNow)
    {
        if (Status is not ReminderStatus.Sent)
        {
            throw new InvalidOperationException($"Cannot Skip reminder in status {Status}");
        }

        Status = ReminderStatus.Skipped;
        ConfirmedAt = utcNow;
    }

    public void MarkFailed(DateTime utcNow)
    {
        if (Status is not ReminderStatus.Sending)
        {
            throw new InvalidOperationException($"Cannot MarkFailed reminder in status {Status}");
        }

        Status = ReminderStatus.Failed;
        ConfirmedAt = utcNow;
    }

    public void Cancel(DateTime utcNow)
    {
        if (Status is not (ReminderStatus.Pending or ReminderStatus.Sending))
        {
            throw new InvalidOperationException($"Cannot Cancel reminder in status {Status}");
        }

        Status = ReminderStatus.Cancelled;
        ConfirmedAt = utcNow;
    }
}