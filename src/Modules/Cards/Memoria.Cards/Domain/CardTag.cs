namespace Memoria.Cards.Domain;

/// <summary>
/// Join-entity для связи карточка→тег. Hard-delete: при soft-delete карточки
/// записи остаются, при permanent-delete — удаляются вместе с карточкой.
/// </summary>
internal sealed class CardTag
{
    private CardTag()
    {
    }

    public CardTag(Guid cardId, Guid tagId)
    {
        CardId = cardId;
        TagId = tagId;
    }

    public Guid CardId { get; private set; }
    public Guid TagId { get; private set; }

    /// <summary>
    /// Repoints this join row at a different tag. Used only by the
    /// account-merge flow when collapsing duplicate tag names.
    /// </summary>
    internal void RepointToTag(Guid newTagId) => TagId = newTagId;
}
