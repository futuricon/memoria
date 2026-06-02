namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// First two steps of the activation funnel: signed-up users and how many
/// of them have linked a Telegram identity. Reads off the Users module.
/// </summary>
public sealed record SignupAndLinkCountsDto(
    int TotalSignups,
    int TelegramLinked);
