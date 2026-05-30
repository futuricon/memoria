using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Domain;
using Memoria.Users.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Users.Features.CompleteTelegramLinking;

internal sealed class CompleteTelegramLinkingCommandHandler
    : IRequestHandler<CompleteTelegramLinkingCommand, Result<TelegramLinkingResultDto>>
{
    private readonly UsersDbContext _db;
    private readonly IMediator _mediator;
    private readonly TimeProvider _clock;

    public CompleteTelegramLinkingCommandHandler(
        UsersDbContext db, IMediator mediator, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _mediator = mediator;
        _clock = clock;
    }

    public async Task<Result<TelegramLinkingResultDto>> Handle(
        CompleteTelegramLinkingCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.GetUtcNow().UtcDateTime;

        var verification = await _db.VerificationCodes
            .FirstOrDefaultAsync(
                c => c.TargetIdentifier == request.Token
                     && c.Purpose == VerificationPurpose.LinkTelegram,
                cancellationToken)
            .ConfigureAwait(false);

        if (verification is null)
        {
            return Result<TelegramLinkingResultDto>.Failure(Error.NotFound(
                "users.linking_token_unknown", "Unknown linking token."));
        }

        if (!verification.IsActive(now))
        {
            return Result<TelegramLinkingResultDto>.Failure(Error.Validation(
                "users.linking_token_expired", "Linking token is expired or already used."));
        }

        if (verification.UserId is null)
        {
            return Result<TelegramLinkingResultDto>.Failure(Error.Unexpected(
                "users.linking_token_orphan", "Linking token has no associated user."));
        }

        var targetUserId = verification.UserId.Value;

        var existing = await _db.Identities
            .FirstOrDefaultAsync(
                i => i.Provider == IdentityProvider.Telegram && i.ExternalId == request.TelegramId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            // Fresh link — attach a new Telegram identity to the target user.
            _db.Identities.Add(new UserIdentity(
                userId: targetUserId,
                provider: IdentityProvider.Telegram,
                externalId: request.TelegramId,
                linkedAt: now));
            verification.MarkConsumed(now);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<TelegramLinkingResultDto>.Success(
                new TelegramLinkingResultDto(Merged: false, MergeStats: null));
        }

        if (existing.UserId == targetUserId)
        {
            // Idempotent re-tap of the deep-link by the same user. No work,
            // still consume the token so it can't be replayed.
            verification.MarkConsumed(now);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result<TelegramLinkingResultDto>.Success(
                new TelegramLinkingResultDto(Merged: false, MergeStats: null));
        }

        // The Telegram chat is already attached to a DIFFERENT user (almost
        // always the bot's auto-registered account). Merge that account into
        // the SPA-authenticated target. The merge handler repoints the
        // Telegram identity to the target as part of identity cleanup, so we
        // don't need to add a new identity row here.
        var merge = await _mediator
            .Send(new MergeAccountsCommand(
                SourceUserId: existing.UserId,
                TargetUserId: targetUserId), cancellationToken)
            .ConfigureAwait(false);

        if (merge.IsFailure)
        {
            return Result<TelegramLinkingResultDto>.Failure(merge.Error!);
        }

        verification.MarkConsumed(now);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<TelegramLinkingResultDto>.Success(
            new TelegramLinkingResultDto(Merged: true, MergeStats: merge.Value));
    }
}
