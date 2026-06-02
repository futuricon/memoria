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

namespace Memoria.Users.Features.ExchangeBotLoginCode;

internal sealed class ExchangeBotLoginCodeCommandHandler
    : IRequestHandler<ExchangeBotLoginCodeCommand, Result<JwtTokenPairDto>>
{
    private readonly UsersDbContext _db;
    private readonly VerificationCodeService _codes;
    private readonly JwtTokenIssuer _jwt;
    private readonly TimeProvider _clock;
    private readonly VerificationCodeOptions _options;
    private readonly AdminOptions _adminOptions;

    public ExchangeBotLoginCodeCommandHandler(
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
        ExchangeBotLoginCodeCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = _clock.GetUtcNow().UtcDateTime;

        var candidates = await _db.VerificationCodes
            .Where(c => c.Purpose == VerificationPurpose.LoginViaBotCode
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
                "users.login_code_invalid", "Login code is invalid or expired."));
        }

        if (match.UserId is null)
        {
            return Result<JwtTokenPairDto>.Failure(Error.Unexpected(
                "users.login_code_orphan", "Login code has no associated user."));
        }

        if (match.AttemptCount >= _options.MaxAttempts)
        {
            return Result<JwtTokenPairDto>.Failure(Error.Forbidden(
                "users.login_code_locked", "Too many failed attempts."));
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == match.UserId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return Result<JwtTokenPairDto>.Failure(Error.NotFound(
                "users.not_found", "User not found."));
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
