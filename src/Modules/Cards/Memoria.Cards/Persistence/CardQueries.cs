using Microsoft.EntityFrameworkCore;

using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Domain;

namespace Memoria.Cards.Persistence;

/// <summary>
/// Шорткаты для повторяющихся EF-запросов поверх <see cref="CardsDbContext"/>.
/// </summary>
internal static class CardQueries
{
    /// <summary>
    /// Доступ к карточкам, включая soft-deleted (через <c>IgnoreQueryFilters</c>).
    /// </summary>
    public static IQueryable<Card> IncludingDeleted(this DbSet<Card> set) =>
        set.IgnoreQueryFilters();

    /// <summary>
    /// Только soft-deleted карточки.
    /// </summary>
    public static IQueryable<Card> OnlyDeleted(this DbSet<Card> set) =>
        set.IgnoreQueryFilters().Where(c => c.DeletedAt != null);

    /// <summary>
    /// Загружает теги карточки в одной круговой query (без N+1).
    /// </summary>
    public static async Task<IReadOnlyList<string>> LoadTagsForCardAsync(
        this CardsDbContext db,
        Guid cardId,
        CancellationToken ct)
    {
        var tags = await (
            from ct2 in db.CardTags
            join t in db.Tags on ct2.TagId equals t.Id
            where ct2.CardId == cardId
            orderby t.NormalizedName
            select t.NormalizedName).ToListAsync(ct).ConfigureAwait(false);
        return tags;
    }

    public static CardDto ToDto(Card card, IReadOnlyList<string> tags) =>
        new(card.Id, card.Title, card.Body, tags, card.CreatedAt, card.UpdatedAt, card.Type,
            IsPaused: card.IsPaused, PausedAtStage: card.PausedAtStage);
}
