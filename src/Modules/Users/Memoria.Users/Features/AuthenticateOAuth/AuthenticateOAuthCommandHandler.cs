using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Domain;
using Memoria.Users.Persistence;
using Memoria.Users.Services;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Users.Features.AuthenticateOAuth;

/// <summary>
/// OAuth sign-in for SPA Google/GitHub flows. Resolution order mirrors
/// CompleteEmailLinking:
/// <list type="number">
///   <item>existing UserIdentity (provider, externalId) → log in</item>
///   <item>verified email matches an existing user → attach a new identity</item>
///   <item>otherwise → register a fresh user</item>
/// </list>
/// Unverified emails are NEVER used for linking — they only seed
/// <c>User.Email</c> on a newly created account.
/// </summary>
internal sealed class AuthenticateOAuthCommandHandler
    : IRequestHandler<AuthenticateOAuthCommand, Result<JwtTokenPairDto>>
{
    private readonly UsersDbContext _db;
    private readonly JwtTokenIssuer _jwt;
    private readonly TimeProvider _clock;

    public AuthenticateOAuthCommandHandler(UsersDbContext db, JwtTokenIssuer jwt, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(jwt);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _jwt = jwt;
        _clock = clock;
    }

    public async Task<Result<JwtTokenPairDto>> Handle(
        AuthenticateOAuthCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.TryParse<IdentityProvider>(request.Provider, ignoreCase: false, out var provider)
            || provider is IdentityProvider.Email or IdentityProvider.Telegram)
        {
            return Result<JwtTokenPairDto>.Failure(Error.Validation(
                "users.oauth_provider_unknown",
                "Unknown OAuth provider."));
        }

        if (string.IsNullOrWhiteSpace(request.ExternalId))
        {
            return Result<JwtTokenPairDto>.Failure(Error.Validation(
                "users.oauth_external_id_missing",
                "OAuth provider did not return a stable user id."));
        }

        var now = _clock.GetUtcNow().UtcDateTime;

        var existingIdentity = await _db.Identities
            .FirstOrDefaultAsync(
                i => i.Provider == provider && i.ExternalId == request.ExternalId,
                ct)
            .ConfigureAwait(false);

        User user;
        if (existingIdentity is not null)
        {
            var found = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == existingIdentity.UserId, ct)
                .ConfigureAwait(false);

            if (found is null)
            {
                return Result<JwtTokenPairDto>.Failure(Error.NotFound(
                    "users.not_found",
                    "User linked to this OAuth identity is missing."));
            }

            user = found;
        }
        else
        {
            // Cross-provider link by verified email. Unverified emails would
            // let an attacker who can spoof emails (or pre-claim providers
            // that don't verify) take over an existing account, so we require
            // EmailVerified=true to bridge identities.
            User? matchByEmail = null;
            if (request.EmailVerified && !string.IsNullOrWhiteSpace(request.Email))
            {
                matchByEmail = await _db.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Email, ct)
                    .ConfigureAwait(false);
            }

            if (matchByEmail is not null)
            {
                user = matchByEmail;
                _db.Identities.Add(new UserIdentity(
                    userId: user.Id,
                    provider: provider,
                    externalId: request.ExternalId,
                    linkedAt: now));
            }
            else
            {
                user = new User(
                    displayName: string.IsNullOrWhiteSpace(request.DisplayName)
                        ? request.Email ?? request.Provider
                        : request.DisplayName,
                    timeZoneId: "UTC",
                    createdAt: now);

                if (request.EmailVerified && !string.IsNullOrWhiteSpace(request.Email))
                {
                    user.SetEmail(request.Email);
                }

                _db.Users.Add(user);
                _db.Identities.Add(new UserIdentity(
                    userId: user.Id,
                    provider: provider,
                    externalId: request.ExternalId,
                    linkedAt: now));
            }
        }

        var pair = _jwt.Issue(user);
        _db.RefreshTokens.Add(new RefreshToken(
            userId: user.Id,
            tokenHash: _jwt.HashRefreshToken(pair.RefreshToken),
            expiresAt: pair.RefreshExpiresAt,
            createdAt: now));

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new JwtTokenPairDto(
            pair.AccessToken,
            pair.AccessExpiresAt,
            pair.RefreshToken,
            pair.RefreshExpiresAt);
    }
}
