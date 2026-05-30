using MediatR;

using Memoria.Cards.Contracts.Commands;
using Memoria.Cards.Persistence;
using Memoria.Shared.Kernel.Results;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Cards.Features.ReassignCardsOwner;

/// <summary>
/// Moves every <c>Card</c> (active and soft-deleted) owned by
/// <c>SourceUserId</c> to <c>TargetUserId</c>, then dedupes tags by
/// <c>NormalizedName</c>:
/// <list type="bullet">
///   <item>collision (target has same name) → repoint Source's
///         <c>CardTag</c> rows at Target's tag, then delete Source's tag</item>
///   <item>no collision → simply reassign the Source tag to Target</item>
/// </list>
/// Idempotent — re-running after a partial failure matches no rows.
/// Returns the number of cards moved.
/// </summary>
internal sealed class ReassignCardsOwnerCommandHandler
    : IRequestHandler<ReassignCardsOwnerCommand, Result<int>>
{
    private readonly CardsDbContext _db;

    public ReassignCardsOwnerCommandHandler(CardsDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<int>> Handle(ReassignCardsOwnerCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SourceUserId == request.TargetUserId)
        {
            return Result<int>.Success(0);
        }

        // Dedupe tags FIRST: walk source's tags, decide collision vs handover.
        // Done before cards are reassigned because both target's existing tag
        // and source's not-yet-reassigned tag still resolve correctly here.
        var sourceTags = await _db.Tags
            .IgnoreQueryFilters()
            .Where(t => t.UserId == request.SourceUserId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (sourceTags.Count > 0)
        {
            var targetTagsByName = await _db.Tags
                .IgnoreQueryFilters()
                .Where(t => t.UserId == request.TargetUserId)
                .ToDictionaryAsync(t => t.NormalizedName, ct)
                .ConfigureAwait(false);

            foreach (var sourceTag in sourceTags)
            {
                if (targetTagsByName.TryGetValue(sourceTag.NormalizedName, out var targetTag))
                {
                    // CardTag has a composite primary key on (CardId, TagId),
                    // so we can't update TagId in place — drop the old join
                    // and insert a fresh one. Dedupe against any join that
                    // already binds the card to the target tag.
                    var joins = await _db.CardTags
                        .Where(ct2 => ct2.TagId == sourceTag.Id)
                        .ToListAsync(ct)
                        .ConfigureAwait(false);

                    var alreadyOnTarget = new HashSet<Guid>(
                        await _db.CardTags
                            .Where(ct2 => ct2.TagId == targetTag.Id)
                            .Select(ct2 => ct2.CardId)
                            .ToListAsync(ct)
                            .ConfigureAwait(false));

                    foreach (var join in joins)
                    {
                        _db.CardTags.Remove(join);
                        if (alreadyOnTarget.Add(join.CardId))
                        {
                            _db.CardTags.Add(new Domain.CardTag(join.CardId, targetTag.Id));
                        }
                    }

                    _db.Tags.Remove(sourceTag);
                }
                else
                {
                    sourceTag.ReassignTo(request.TargetUserId);
                }
            }
        }

        // Reassign cards (active + soft-deleted) — soft-deleted ones land in
        // Target's trash without rehydration.
        var cards = await _db.Cards
            .IgnoreQueryFilters()
            .Where(c => c.UserId == request.SourceUserId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var card in cards)
        {
            card.ReassignTo(request.TargetUserId);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result<int>.Success(cards.Count);
    }
}
