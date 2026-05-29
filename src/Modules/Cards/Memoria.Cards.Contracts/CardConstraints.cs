namespace Memoria.Cards.Contracts;

/// <summary>
/// Single source of truth for card field limits. Referenced by validators, EF
/// configuration, and the bot FSM so the numbers never drift across layers.
/// </summary>
public static class CardConstraints
{
    public const int MaxTitleLength = 200;
    public const int MaxBodyLength = 4000;
    public const int TagMinLength = 2;
    public const int TagMaxLength = 30;
    public const int MaxTagsPerCard = 5;
}
