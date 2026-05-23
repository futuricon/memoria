using MediatR;
using Microsoft.EntityFrameworkCore;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Domain;
using Memoria.Users.Persistence;
using Memoria.Users.Services;

namespace Memoria.Users.Features.RefreshAccessToken;

internal sealed class RefreshAccessTokenCommandHandler
    : IRequestHandler<RefreshAccessTokenCommand, Result<JwtTokenPairDto>>
{
    private readonly UsersDbContext _db;
    private readonly JwtTokenIssuer _jwt;
    private readonly TimeProvider _clock;

    public RefreshAccessTokenCommandHandler(UsersDbContext db, JwtTokenIssuer jwt, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(jwt);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _jwt = jwt;
        _clock = clock;
    }

    public async Task<Result<JwtTokenPairDto>> Handle(
        RefreshAccessTokenCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tokenHash = _jwt.HashRefreshToken(request.RefreshToken);
        var now = _clock.GetUtcNow().UtcDateTime;

        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return Result<JwtTokenPairDto>.Failure(Error.Unauthorized(
                "users.refresh_unknown", "Refresh token is unknown."));
        }

        if (!stored.IsActive(now))
        {
            return Result<JwtTokenPairDto>.Failure(Error.Unauthorized(
                "users.refresh_inactive", "Refresh token is expired or revoked."));
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == stored.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return Result<JwtTokenPairDto>.Failure(Error.NotFound(
                "users.not_found", "User not found."));
        }

        var pair = _jwt.Issue(user);
        var newToken = new RefreshToken(
            userId: user.Id,
            tokenHash: _jwt.HashRefreshToken(pair.RefreshToken),
            expiresAt: pair.RefreshExpiresAt,
            createdAt: now);

        _db.RefreshTokens.Add(newToken);
        stored.Revoke(now, newToken.Id);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new JwtTokenPairDto(
            pair.AccessToken,
            pair.AccessExpiresAt,
            pair.RefreshToken,
            pair.RefreshExpiresAt);
    }
}
