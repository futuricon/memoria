using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Memoria.Shared.Infrastructure.Options;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Domain;
using Memoria.Users.Options;
using Memoria.Users.Persistence;
using Memoria.Users.Services;

namespace Memoria.Users.Features.CompleteEmailLinking;

internal sealed class CompleteEmailLinkingCommandHandler
    : IRequestHandler<CompleteEmailLinkingCommand, Result<JwtTokenPairDto>>
{
    private readonly UsersDbContext _db;
    private readonly VerificationCodeService _codes;
    private readonly JwtTokenIssuer _jwt;
    private readonly TimeProvider _clock;
    private readonly VerificationCodeOptions _options;
    private readonly AdminOptions _adminOptions;

    public CompleteEmailLinkingCommandHandler(
        UsersDbContext db,
        VerificationCodeService codes,
        JwtTokenIssuer jwt,
        TimeProvider clock,
        IOptions<VerificationCodeOptions> options,
        IOptions<AdminOptions> adminOptions)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(jwt);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(adminOptions);
        _db = db;
        _codes = codes;
        _jwt = jwt;
        _clock = clock;
        _options = options.Value;
        _adminOptions = adminOptions.Value;
    }

    public async Task<Result<JwtTokenPairDto>> Handle(
        CompleteEmailLinkingCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.GetUtcNow().UtcDateTime;

        var candidates = await _db.VerificationCodes
            .Where(c => c.Purpose == VerificationPurpose.LinkEmail
                        && c.TargetIdentifier == request.Email
                        && c.ConsumedAt == null
                        && c.ExpiresAt > now)
            .OrderByDescending(c => c.ExpiresAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var match = candidates.FirstOrDefault(c => _codes.Verify(request.Code, c.CodeHash));

        if (match is null)
        {
            foreach (var stale in candidates)
            {
                stale.RegisterAttempt();
                if (stale.AttemptCount >= _options.MaxAttempts)
                {
                    stale.MarkConsumed(now);
                }
            }
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Result<JwtTokenPairDto>.Failure(Error.Validation(
                "users.email_code_invalid", "Email code is invalid or expired."));
        }

        if (match.AttemptCount >= _options.MaxAttempts)
        {
            return Result<JwtTokenPairDto>.Failure(Error.Forbidden(
                "users.email_code_locked", "Too many failed attempts."));
        }

        var user = match.UserId is not null
            ? await _db.Users.FirstOrDefaultAsync(u => u.Id == match.UserId.Value, cancellationToken)
                .ConfigureAwait(false)
            : null;

        if (match.UserId is not null && user is null)
        {
            return Result<JwtTokenPairDto>.Failure(Error.NotFound(
                "users.not_found", "User not found."));
        }

        if (user is null)
        {
            // SPA "sign in by email" flow: the verification code was issued
            // without a UserId. Three possible states for this email address:
            //   (a) UserIdentity (Email, X) already exists → log in to its owner
            //   (b) User.Email = X but no Email-provider identity yet → attach,
            //       then log in (covers users created via Telegram who later
            //       linked their email manually or via the bot)
            //   (c) Neither exists → register a brand-new user
            // Ownership of the inbox is proof of authorization for all three.
            var existingIdentity = await _db.Identities
                .FirstOrDefaultAsync(
                    i => i.Provider == IdentityProvider.Email && i.ExternalId == request.Email,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existingIdentity is not null)
            {
                user = await _db.Users
                    .FirstOrDefaultAsync(u => u.Id == existingIdentity.UserId, cancellationToken)
                    .ConfigureAwait(false);
                if (user is null)
                {
                    return Result<JwtTokenPairDto>.Failure(Error.NotFound(
                        "users.not_found", "User not found."));
                }
            }
            else
            {
                user = await _db.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken)
                    .ConfigureAwait(false);

                if (user is null)
                {
                    user = new User(
                        displayName: request.Email,
                        timeZoneId: "UTC",
                        createdAt: now);
                    user.SetEmail(request.Email);
                    _db.Users.Add(user);
                }

                _db.Identities.Add(new UserIdentity(
                    userId: user.Id,
                    provider: IdentityProvider.Email,
                    externalId: request.Email,
                    linkedAt: now));
            }
        }
        else if (string.IsNullOrEmpty(user.Email))
        {
            user.SetEmail(request.Email);

            var alreadyLinked = await _db.Identities
                .AnyAsync(
                    i => i.UserId == user.Id && i.Provider == IdentityProvider.Email,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!alreadyLinked)
            {
                _db.Identities.Add(new UserIdentity(
                    userId: user.Id,
                    provider: IdentityProvider.Email,
                    externalId: request.Email,
                    linkedAt: now));
            }
        }

        match.MarkConsumed(now);
        user.MarkTokenIssued(now, _adminOptions.Emails);
        var pair = _jwt.Issue(user);
        var refreshTokenEntity = new RefreshToken(
            userId: user.Id,
            tokenHash: _jwt.HashRefreshToken(pair.RefreshToken),
            expiresAt: pair.RefreshExpiresAt,
            createdAt: now);

        _db.RefreshTokens.Add(refreshTokenEntity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new JwtTokenPairDto(
            pair.AccessToken,
            pair.AccessExpiresAt,
            pair.RefreshToken,
            pair.RefreshExpiresAt);
    }
}
