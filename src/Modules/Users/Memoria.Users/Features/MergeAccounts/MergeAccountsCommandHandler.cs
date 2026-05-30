using MediatR;

using Memoria.Cards.Contracts.Commands;
using Memoria.Reminders.Contracts.Commands;
using Memoria.Reviews.Contracts.Commands;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using CardsReassign = Memoria.Cards.Contracts.Commands.ReassignCardsOwnerCommand;
using RemindersCancel = Memoria.Reminders.Contracts.Commands.CancelRemindersForUserCommand;
using RemindersReassign = Memoria.Reminders.Contracts.Commands.ReassignRemindersOwnerCommand;
using ReviewsReassign = Memoria.Reviews.Contracts.Commands.ReassignReviewsOwnerCommand;

namespace Memoria.Users.Features.MergeAccounts;

/// <summary>
/// Orchestrates account merge across Cards / Reminders / Reviews modules
/// then mops up Users-owned data (identities, refresh tokens, verification
/// codes, the user row itself). Each cross-module step is idempotent so a
/// retry after partial failure converges to the final state.
/// </summary>
internal sealed class MergeAccountsCommandHandler
    : IRequestHandler<MergeAccountsCommand, Result<MergeAccountsResultDto>>
{
    private readonly UsersDbContext _db;
    private readonly IMediator _mediator;
    private readonly TimeProvider _clock;
    private readonly ILogger<MergeAccountsCommandHandler> _logger;

    public MergeAccountsCommandHandler(
        UsersDbContext db,
        IMediator mediator,
        TimeProvider clock,
        ILogger<MergeAccountsCommandHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);
        _db = db;
        _mediator = mediator;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<MergeAccountsResultDto>> Handle(
        MergeAccountsCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SourceUserId == request.TargetUserId)
        {
            return Result<MergeAccountsResultDto>.Failure(Error.Validation(
                "users.merge_same_user", "Source and target users are the same."));
        }

        var source = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == request.SourceUserId, ct)
            .ConfigureAwait(false);
        var target = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.TargetUserId, ct)
            .ConfigureAwait(false);

        if (source is null)
        {
            return Result<MergeAccountsResultDto>.Failure(Error.NotFound(
                "users.merge_source_not_found", "Source user not found."));
        }
        if (target is null)
        {
            return Result<MergeAccountsResultDto>.Failure(Error.NotFound(
                "users.merge_target_not_found", "Target user not found or already deleted."));
        }

        // 1. Cancel source's in-flight reminders BEFORE reassigning ownership —
        //    cancellation logic queries by UserId, so we must hit those rows
        //    while they still belong to source.
        var cancel = await _mediator
            .Send(new RemindersCancel(request.SourceUserId), ct)
            .ConfigureAwait(false);
        if (cancel.IsFailure)
        {
            return Result<MergeAccountsResultDto>.Failure(cancel.Error!);
        }

        // 2. Reassign cards (incl. tag dedupe).
        var cardsResult = await _mediator
            .Send(new CardsReassign(request.SourceUserId, request.TargetUserId), ct)
            .ConfigureAwait(false);
        if (cardsResult.IsFailure)
        {
            return Result<MergeAccountsResultDto>.Failure(cardsResult.Error!);
        }

        // 3. Reassign remaining reminders (the Cancelled ones from step 1
        //    keep their history but become Target's).
        var remindersResult = await _mediator
            .Send(new RemindersReassign(request.SourceUserId, request.TargetUserId), ct)
            .ConfigureAwait(false);
        if (remindersResult.IsFailure)
        {
            return Result<MergeAccountsResultDto>.Failure(remindersResult.Error!);
        }

        // 4. Reassign reviews.
        var reviewsResult = await _mediator
            .Send(new ReviewsReassign(request.SourceUserId, request.TargetUserId), ct)
            .ConfigureAwait(false);
        if (reviewsResult.IsFailure)
        {
            return Result<MergeAccountsResultDto>.Failure(reviewsResult.Error!);
        }

        // 5. Users-side cleanup in a single SaveChanges.
        await CleanupUsersSideAsync(request.SourceUserId, request.TargetUserId, ct).ConfigureAwait(false);

        var stats = new MergeAccountsResultDto(
            CardsMoved: cardsResult.Value,
            RemindersMoved: remindersResult.Value,
            ReviewsMoved: reviewsResult.Value);

        _logger.LogInformation(
            "Merged user {SourceId} into {TargetId}: {Cards} cards, {Reminders} reminders, {Reviews} reviews",
            request.SourceUserId, request.TargetUserId,
            stats.CardsMoved, stats.RemindersMoved, stats.ReviewsMoved);

        return Result<MergeAccountsResultDto>.Success(stats);
    }

    private async Task CleanupUsersSideAsync(Guid sourceId, Guid targetId, CancellationToken ct)
    {
        var sourceIdentities = await _db.Identities
            .Where(i => i.UserId == sourceId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (sourceIdentities.Count > 0)
        {
            // Collisions: target already owns (provider, externalId) — drop source's row.
            // Non-collisions: hand the identity over.
            var targetKeys = new HashSet<(Domain.IdentityProvider, string)>(
                await _db.Identities
                    .Where(i => i.UserId == targetId)
                    .Select(i => new ValueTuple<Domain.IdentityProvider, string>(i.Provider, i.ExternalId))
                    .ToListAsync(ct)
                    .ConfigureAwait(false));

            foreach (var identity in sourceIdentities)
            {
                if (targetKeys.Contains((identity.Provider, identity.ExternalId)))
                {
                    _db.Identities.Remove(identity);
                }
                else
                {
                    identity.ReassignTo(targetId);
                    targetKeys.Add((identity.Provider, identity.ExternalId));
                }
            }
        }

        var refreshTokens = await _db.RefreshTokens
            .Where(t => t.UserId == sourceId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        _db.RefreshTokens.RemoveRange(refreshTokens);

        var codes = await _db.VerificationCodes
            .Where(c => c.UserId == sourceId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        _db.VerificationCodes.RemoveRange(codes);

        var source = await _db.Users
            .IgnoreQueryFilters()
            .FirstAsync(u => u.Id == sourceId, ct)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(source.Email))
        {
            source.ClearEmail();
        }

        if (source.DeletedAt is null)
        {
            source.SoftDelete(_clock.GetUtcNow().UtcDateTime);
        }

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
