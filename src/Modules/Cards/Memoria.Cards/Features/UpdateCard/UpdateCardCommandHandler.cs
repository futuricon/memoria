using MediatR;
using Microsoft.EntityFrameworkCore;

using Memoria.Cards.Contracts;
using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Domain;
using Memoria.Cards.Persistence;
using Memoria.Cards.Services;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Features.UpdateCard;

internal sealed class UpdateCardCommandHandler : IRequestHandler<UpdateCardCommand, Result<CardDto>>
{
    private readonly CardsDbContext _db;
    private readonly TagNormalizer _normalizer;
    private readonly TagRepository _tags;
    private readonly TimeProvider _clock;

    public UpdateCardCommandHandler(
        CardsDbContext db,
        TagNormalizer normalizer,
        TagRepository tags,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _normalizer = normalizer;
        _tags = tags;
        _clock = clock;
    }

    public async Task<Result<CardDto>> Handle(UpdateCardCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var card = await _db.Cards
            .FirstOrDefaultAsync(c => c.Id == request.CardId, cancellationToken)
            .ConfigureAwait(false);

        if (card is null)
        {
            return Result<CardDto>.Failure(Error.NotFound("cards.not_found", "Card not found."));
        }

        if (card.UserId != request.UserId)
        {
            return Result<CardDto>.Failure(Error.Forbidden(
                "cards.not_owner", "Card belongs to another user."));
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        card.Edit(request.Title, request.Body, now);

        IReadOnlyList<string> tagsForDto;
        if (request.Tags is not null)
        {
            var normalized = _normalizer.NormalizeMany(request.Tags);
            if (normalized.IsFailure)
            {
                return Result<CardDto>.Failure(normalized.Error!);
            }

            if (normalized.Value!.Count > CardConstraints.MaxTagsPerCard)
            {
                return Result<CardDto>.Failure(Error.Validation(
                    "cards.too_many_tags",
                    $"A card can have at most {CardConstraints.MaxTagsPerCard} tags."));
            }

            var oldLinks = await _db.CardTags
                .Where(ct => ct.CardId == card.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            _db.CardTags.RemoveRange(oldLinks);

            var tagIds = await _tags.EnsureTagsAsync(card.UserId, normalized.Value!, cancellationToken)
                .ConfigureAwait(false);
            foreach (var tagId in tagIds)
            {
                _db.CardTags.Add(new CardTag(card.Id, tagId));
            }

            tagsForDto = normalized.Value!;
        }
        else
        {
            tagsForDto = await _db.LoadTagsForCardAsync(card.Id, cancellationToken).ConfigureAwait(false);
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CardQueries.ToDto(card, tagsForDto);
    }
}
