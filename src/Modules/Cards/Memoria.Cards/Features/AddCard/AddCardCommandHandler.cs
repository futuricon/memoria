using MediatR;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Events;
using Memoria.Cards.Domain;
using Memoria.Cards.Persistence;
using Memoria.Cards.Services;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Features.AddCard;

internal sealed class AddCardCommandHandler : IRequestHandler<AddCardCommand, Result<CardDto>>
{
    private readonly CardsDbContext _db;
    private readonly TagNormalizer _normalizer;
    private readonly TagRepository _tags;
    private readonly TimeProvider _clock;
    private readonly IPublisher _publisher;

    public AddCardCommandHandler(
        CardsDbContext db,
        TagNormalizer normalizer,
        TagRepository tags,
        TimeProvider clock,
        IPublisher publisher)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(publisher);
        _db = db;
        _normalizer = normalizer;
        _tags = tags;
        _clock = clock;
        _publisher = publisher;
    }

    public async Task<Result<CardDto>> Handle(AddCardCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = _normalizer.NormalizeMany(request.Tags);
        if (normalized.IsFailure)
        {
            return Result<CardDto>.Failure(normalized.Error!);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        var card = new Card(request.UserId, request.Title, request.Body, now);
        _db.Cards.Add(card);

        var tagIds = await _tags.EnsureTagsAsync(request.UserId, normalized.Value!, cancellationToken)
            .ConfigureAwait(false);
        foreach (var tagId in tagIds)
        {
            _db.CardTags.Add(new CardTag(card.Id, tagId));
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _publisher.Publish(new CardCreatedEvent(card.Id, card.UserId, card.CreatedAt), cancellationToken)
            .ConfigureAwait(false);

        return CardQueries.ToDto(card, normalized.Value!);
    }
}
