namespace Memoria.Reminders.Contracts.Abstractions;

public sealed record ReminderNotification(
    Guid ReminderId,
    Guid UserId,
    Guid CardId,
    string CardTitle,
    string CardBody,
    IReadOnlyList<string> Tags,
    int StageNumber);