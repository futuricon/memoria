namespace Memoria.Reviews.Domain;

/// <summary>
/// Append-only record of a single recall attempt. Per addendum §3 stores
/// <see cref="CardTitleSnapshot"/> at creation so the review remains
/// meaningful even after the card is edited or hard-deleted.
/// </summary>
internal sealed class Review
{
    private Review()
    {
    }

    public Review(
        Guid cardId,
        Guid userId,
        Guid? reminderId,
        Rating rating,
        string cardTitleSnapshot,
        DateTime reviewedAt,
        string? note)
    {
        Id = Guid.NewGuid();
        CardId = cardId;
        UserId = userId;
        ReminderId = reminderId;
        Rating = rating;
        CardTitleSnapshot = cardTitleSnapshot;
        ReviewedAt = reviewedAt;
        Note = note;
    }

    public Guid Id { get; private set; }
    public Guid CardId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? ReminderId { get; private set; }
    public Rating Rating { get; private set; }
    public string CardTitleSnapshot { get; private set; } = string.Empty;
    public DateTime ReviewedAt { get; private set; }
    public string? Note { get; private set; }
}
