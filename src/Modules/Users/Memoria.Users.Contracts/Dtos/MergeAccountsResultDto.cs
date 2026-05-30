namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// Outcome of a merge — counts of rows moved from source to target. Used to
/// craft user-facing replies (e.g., bot says "Merged N cards from your old
/// Telegram-only account").
/// </summary>
public sealed record MergeAccountsResultDto(
    int CardsMoved,
    int RemindersMoved,
    int ReviewsMoved);
