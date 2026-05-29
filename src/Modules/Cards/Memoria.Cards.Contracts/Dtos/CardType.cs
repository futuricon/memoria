namespace Memoria.Cards.Contracts.Dtos;

/// <summary>
/// How a card is reviewed. Determined automatically from the title at creation
/// (see the Cards domain resolver) and fixed for the card's lifetime.
/// </summary>
public enum CardType
{
    /// <summary>Title → "Show answer" → Body, user self-rates recall.</summary>
    Note,

    /// <summary>Title is a question, Body the reference answer; the user types
    /// an answer that is graded automatically.</summary>
    Question,
}
