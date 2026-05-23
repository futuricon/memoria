using MediatR;
using Microsoft.EntityFrameworkCore;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Domain;
using Memoria.Users.Persistence;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Users.Features.CompleteTelegramLinking;

internal sealed class CompleteTelegramLinkingCommandHandler
    : IRequestHandler<CompleteTelegramLinkingCommand, Result<Unit>>
{
    private readonly UsersDbContext _db;
    private readonly TimeProvider _clock;

    public CompleteTelegramLinkingCommandHandler(UsersDbContext db, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _clock = clock;
    }

    public async Task<Result<Unit>> Handle(
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
            return Result<Unit>.Failure(Error.NotFound(
                "users.linking_token_unknown", "Unknown linking token."));
        }

        if (!verification.IsActive(now))
        {
            return Result<Unit>.Failure(Error.Validation(
                "users.linking_token_expired", "Linking token is expired or already used."));
        }

        if (verification.UserId is null)
        {
            return Result<Unit>.Failure(Error.Unexpected(
                "users.linking_token_orphan", "Linking token has no associated user."));
        }

        var existing = await _db.Identities
            .FirstOrDefaultAsync(
                i => i.Provider == IdentityProvider.Telegram && i.ExternalId == request.TelegramId,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return Result<Unit>.Failure(Error.Conflict(
                "users.telegram_already_linked",
                "This Telegram account is already linked to another user."));
        }

        var identity = new UserIdentity(
            userId: verification.UserId.Value,
            provider: IdentityProvider.Telegram,
            externalId: request.TelegramId,
            linkedAt: now);

        _db.Identities.Add(identity);
        verification.MarkConsumed(now);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<Unit>.Success(Unit.Value);
    }
}
