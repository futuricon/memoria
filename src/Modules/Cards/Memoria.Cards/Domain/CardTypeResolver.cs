using Memoria.Cards.Contracts.Dtos;

namespace Memoria.Cards.Domain;

/// <summary>
/// Derives a card's <see cref="CardType"/> from its title: a title ending in a
/// question mark (after trimming trailing whitespace) is a
/// <see cref="CardType.Question"/>, everything else a <see cref="CardType.Note"/>.
/// The type is computed once at creation and never recomputed on edit.
/// </summary>
internal static class CardTypeResolver
{
    public static CardType FromTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        return title.TrimEnd().EndsWith('?') ? CardType.Question : CardType.Note;
    }
}
