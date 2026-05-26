namespace Memoria.Reviews.Contracts.Dtos;

/// <summary>
/// User's self-assessed recall quality after a reminder. Part of the public
/// contract — used by callers (bot, API) to express how well the user
/// remembered the card.
/// </summary>
public enum Rating
{
    Forgot,
    Hard,
    Good,
    Easy,
}
