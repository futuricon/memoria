namespace Memoria.Reminders.Contracts.Dtos;

/// <summary>
/// Reminder, due на конкретный день в TZ пользователя. Используется
/// эндпоинтом <c>GET /api/v1/cards/due-today</c>: showcase для SPA, что
/// сегодня нужно повторить.
/// </summary>
public sealed record DueReminderDto(
    Guid ReminderId,
    Guid CardId,
    string CardTitle,
    DateTime ScheduledAt,
    int StageNumber);
