using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Abstractions;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Domain;
using Memoria.Users.Options;
using Memoria.Users.Persistence;
using Memoria.Users.Services;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Users.Features.StartEmailLinking;

internal sealed class StartEmailLinkingCommandHandler
    : IRequestHandler<StartEmailLinkingCommand, Result<Unit>>
{
    private readonly UsersDbContext _db;
    private readonly VerificationCodeService _codes;
    private readonly IEmailSender _email;
    private readonly TimeProvider _clock;
    private readonly VerificationCodeOptions _options;

    public StartEmailLinkingCommandHandler(
        UsersDbContext db,
        VerificationCodeService codes,
        IEmailSender email,
        TimeProvider clock,
        IOptions<VerificationCodeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);
        _db = db;
        _codes = codes;
        _email = email;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<Result<Unit>> Handle(
        StartEmailLinkingCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.UserId is not null)
        {
            var userExists = await _db.Users
                .AnyAsync(u => u.Id == request.UserId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (!userExists)
            {
                return Result<Unit>.Failure(Error.NotFound("users.not_found", "User not found."));
            }
        }

        var code = _codes.GenerateNumericCode();
        var now = _clock.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_options.TtlMinutesForEmail);

        var entity = new VerificationCode(
            userId: request.UserId,
            purpose: VerificationPurpose.LinkEmail,
            targetIdentifier: request.Email,
            codeHash: _codes.Hash(code),
            expiresAt: expiresAt);

        _db.VerificationCodes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _email.SendVerificationCodeAsync(request.Email, code, cancellationToken)
            .ConfigureAwait(false);

        return Result<Unit>.Success(Unit.Value);
    }
}
