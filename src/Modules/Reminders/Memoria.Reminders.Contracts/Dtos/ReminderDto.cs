namespace Memoria.Reminders.Contracts.Dtos;

/// <summary>
/// Минимальная проекция <c>Reminder</c> для callers, которым нужно знать
/// CardId/UserId/Status (например, бот при оценке reminder-а — чтобы записать
/// review против правильной карточки).
/// </summary>
public sealed record ReminderDto(
    Guid Id,
    Guid CardId,
    Guid UserId,
    int StageNumber,
    string Status);
