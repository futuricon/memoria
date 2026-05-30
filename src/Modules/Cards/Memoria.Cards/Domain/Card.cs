using Memoria.Cards.Contracts.Dtos;

namespace Memoria.Cards.Domain;

internal sealed class Card
{
    private readonly List<CardTag> _cardTags = new();

    private Card()
    {
    }

    public Card(Guid userId, string title, string body, DateTime createdAt)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Title = title;
        Body = body;
        Type = CardTypeResolver.FromTitle(title);
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    /// <summary>Derived from the title at creation; never recomputed on edit.</summary>
    public CardType Type { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    /// <summary>
    /// True while the user has explicitly suspended scheduling for this card.
    /// Pending reminders are cancelled on pause and a fresh one is created on
    /// unpause from <see cref="PausedAtStage"/>.
    /// </summary>
    public bool IsPaused { get; private set; }

    /// <summary>
    /// Stage to resume from when the card is unpaused. Captured at pause time
    /// as the highest <c>Reminder.StageNumber</c> seen for the card so the
    /// user picks up where they left off. <c>null</c> for a brand-new card
    /// paused before it ever had a reminder.
    /// </summary>
    public int? PausedAtStage { get; private set; }

    public IReadOnlyCollection<CardTag> CardTags => _cardTags;

    public void Edit(string? title, string? body, DateTime utcNow)
    {
        if (title is not null)
        {
            Title = title;
        }
        if (body is not null)
        {
            Body = body;
        }
        UpdatedAt = utcNow;
    }

    public void SoftDelete(DateTime utcNow) => DeletedAt = utcNow;

    public void Restore(DateTime utcNow)
    {
        DeletedAt = null;
        UpdatedAt = utcNow;
    }

    public void Pause(int? stage, DateTime utcNow)
    {
        if (IsPaused)
        {
            throw new InvalidOperationException("Card is already paused.");
        }

        IsPaused = true;
        PausedAtStage = stage;
        UpdatedAt = utcNow;
    }

    /// <summary>
    /// Returns the stored stage so the caller can hand it to
    /// <c>ReminderScheduler</c>. <c>null</c> means "resume as a fresh card".
    /// </summary>
    public int? Unpause(DateTime utcNow)
    {
        if (!IsPaused)
        {
            throw new InvalidOperationException("Card is not paused.");
        }

        var stage = PausedAtStage;
        IsPaused = false;
        PausedAtStage = null;
        UpdatedAt = utcNow;
        return stage;
    }
}
