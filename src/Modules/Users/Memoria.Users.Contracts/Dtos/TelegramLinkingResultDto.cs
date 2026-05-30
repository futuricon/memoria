namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// Result of <c>CompleteTelegramLinkingCommand</c>. <see cref="Merged"/> is
/// true when the Telegram identity was already attached to a different user
/// and the merge flow ran. <see cref="MergeStats"/> carries the per-module
/// counts so the bot reply can mention them.
/// </summary>
public sealed record TelegramLinkingResultDto(
    bool Merged,
    MergeAccountsResultDto? MergeStats);
