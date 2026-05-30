using Memoria.Reviews.Contracts.Dtos;

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
        string? note,
        string? answerText = null,
        int? aiScore = null,
        string? aiFeedback = null,
        bool autoGraded = false)
    {
        Id = Guid.NewGuid();
        CardId = cardId;
        UserId = userId;
        ReminderId = reminderId;
        Rating = rating;
        CardTitleSnapshot = cardTitleSnapshot;
        ReviewedAt = reviewedAt;
        Note = note;
        AnswerText = answerText;
        AiScore = aiScore;
        AiFeedback = aiFeedback;
        AutoGraded = autoGraded;
    }

    public Guid Id { get; private set; }
    public Guid CardId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? ReminderId { get; private set; }
    public Rating Rating { get; private set; }
    public string CardTitleSnapshot { get; private set; } = string.Empty;
    public DateTime ReviewedAt { get; private set; }
    public string? Note { get; private set; }

    /// <summary>The free-text answer the user submitted (Question cards only).</summary>
    public string? AnswerText { get; private set; }

    /// <summary>AI grade 0–100 (auto-graded Question reviews only).</summary>
    public int? AiScore { get; private set; }

    /// <summary>AI feedback shown to the user (auto-graded Question reviews only).</summary>
    public string? AiFeedback { get; private set; }

    /// <summary>True when the <see cref="Rating"/> was derived from an AI grade.</summary>
    public bool AutoGraded { get; private set; }

    /// <summary>
    /// Moves ownership of this review to a different user. Used only by the
    /// account-merge flow; regular operation never re-parents reviews.
    /// </summary>
    internal void ReassignTo(Guid newUserId) => UserId = newUserId;
}
