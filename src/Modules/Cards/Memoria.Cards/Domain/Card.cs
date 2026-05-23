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
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

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
}
