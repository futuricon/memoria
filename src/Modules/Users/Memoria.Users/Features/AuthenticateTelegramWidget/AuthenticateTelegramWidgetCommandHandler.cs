using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Domain;
using Memoria.Users.Persistence;
using Memoria.Users.Services;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Users.Features.AuthenticateTelegramWidget;

internal sealed class AuthenticateTelegramWidgetCommandHandler
    : IRequestHandler<AuthenticateTelegramWidgetCommand, Result<JwtTokenPairDto>>
{
    private readonly UsersDbContext _db;
    private readonly JwtTokenIssuer _jwt;
    private readonly TimeProvider _clock;

    public AuthenticateTelegramWidgetCommandHandler(
        UsersDbContext db,
        JwtTokenIssuer jwt,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(jwt);
        ArgumentNullException.ThrowIfNull(clock);
        _db = db;
        _jwt = jwt;
        _clock = clock;
    }

    public async Task<Result<JwtTokenPairDto>> Handle(
        AuthenticateTelegramWidgetCommand request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.GetUtcNow().UtcDateTime;

        var identity = await _db.Identities
            .FirstOrDefaultAsync(
                i => i.Provider == IdentityProvider.Telegram && i.ExternalId == request.TelegramId,
                ct)
            .ConfigureAwait(false);

        User user;
        if (identity is not null)
        {
            var existing = await _db.Users
                .FirstOrDefaultAsync(u => u.Id == identity.UserId, ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                return Result<JwtTokenPairDto>.Failure(Error.NotFound(
                    "users.not_found", "User linked to this Telegram identity is missing."));
            }

            user = existing;
        }
        else
        {
            user = new User(displayName: request.DisplayName, timeZoneId: "UTC", createdAt: now);
            var newIdentity = new UserIdentity(
                userId: user.Id,
                provider: IdentityProvider.Telegram,
                externalId: request.TelegramId,
                linkedAt: now);
            _db.Users.Add(user);
            _db.Identities.Add(newIdentity);
        }

        var pair = _jwt.Issue(user);
        var refreshToken = new RefreshToken(
            userId: user.Id,
            tokenHash: _jwt.HashRefreshToken(pair.RefreshToken),
            expiresAt: pair.RefreshExpiresAt,
            createdAt: now);
        _db.RefreshTokens.Add(refreshToken);

        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        return new JwtTokenPairDto(
            pair.AccessToken,
            pair.AccessExpiresAt,
            pair.RefreshToken,
            pair.RefreshExpiresAt);
    }
}
