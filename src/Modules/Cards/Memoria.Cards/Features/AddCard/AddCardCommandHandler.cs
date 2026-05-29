using MediatR;

using Memoria.AI.Contracts.Abstractions;
using Memoria.Cards.Contracts;
using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Contracts.Dtos;
using Memoria.Cards.Contracts.Events;
using Memoria.Cards.Domain;
using Memoria.Cards.Persistence;
using Memoria.Cards.Services;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Logging;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Cards.Features.AddCard;

internal sealed class AddCardCommandHandler : IRequestHandler<AddCardCommand, Result<CardDto>>
{
    private readonly CardsDbContext _db;
    private readonly TagNormalizer _normalizer;
    private readonly TagRepository _tags;
    private readonly TimeProvider _clock;
    private readonly IPublisher _publisher;
    private readonly IQuestionCardValidator _questionValidator;
    private readonly ILogger<AddCardCommandHandler> _logger;

    public AddCardCommandHandler(
        CardsDbContext db,
        TagNormalizer normalizer,
        TagRepository tags,
        TimeProvider clock,
        IPublisher publisher,
        IQuestionCardValidator questionValidator,
        ILogger<AddCardCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(normalizer);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(questionValidator);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _normalizer = normalizer;
        _tags = tags;
        _clock = clock;
        _publisher = publisher;
        _questionValidator = questionValidator;
        _logger = logger;
    }

    public async Task<Result<CardDto>> Handle(AddCardCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

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

        var now = _clock.GetUtcNow().UtcDateTime;
        var card = new Card(request.UserId, request.Title, request.Body, now);

        if (card.Type == CardType.Question)
        {
            var coherence = await EnsureQuestionCoherentAsync(request, cancellationToken).ConfigureAwait(false);
            if (coherence.IsFailure)
            {
                return Result<CardDto>.Failure(coherence.Error!);
            }
        }

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

    /// <summary>
    /// Blocking coherence check for Question cards. A successful "not coherent"
    /// verdict blocks creation; a failed Result (AI unavailable) is fail-open —
    /// we log and let creation proceed so an outage never blocks the user.
    /// </summary>
    private async Task<Result<Unit>> EnsureQuestionCoherentAsync(AddCardCommand request, CancellationToken ct)
    {
        var validation = await _questionValidator
            .ValidateAsync(request.Title, request.Body, ct)
            .ConfigureAwait(false);

        if (validation.IsFailure)
        {
            _logger.LogWarning(
                "Question card coherence check unavailable ({Code}); creating card without it for user {UserId}",
                validation.Error!.Code, request.UserId);
            return Result<Unit>.Success(Unit.Value);
        }

        if (!validation.Value!.IsCoherent)
        {
            return Result<Unit>.Failure(Error.Validation(
                "cards.question_body_mismatch",
                validation.Value.Reason ?? "The answer (body) does not match the question."));
        }

        return Result<Unit>.Success(Unit.Value);
    }
}
