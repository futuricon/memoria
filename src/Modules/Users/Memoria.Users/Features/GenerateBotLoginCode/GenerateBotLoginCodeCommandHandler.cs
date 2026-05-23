using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Domain;
using Memoria.Users.Options;
using Memoria.Users.Persistence;
using Memoria.Users.Services;

namespace Memoria.Users.Features.GenerateBotLoginCode;

internal sealed class GenerateBotLoginCodeCommandHandler
    : IRequestHandler<GenerateBotLoginCodeCommand, Result<string>>
{
    private readonly UsersDbContext _db;
    private readonly VerificationCodeService _codes;
    private readonly TimeProvider _clock;
    private readonly VerificationCodeOptions _options;

    public GenerateBotLoginCodeCommandHandler(
        UsersDbContext db,
        VerificationCodeService codes,
        TimeProvider clock,
        IOptions<VerificationCodeOptions> options)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);
        _db = db;
        _codes = codes;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<Result<string>> Handle(
        GenerateBotLoginCodeCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userExists = await _db.Users
            .AnyAsync(u => u.Id == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!userExists)
        {
            return Result<string>.Failure(Error.NotFound("users.not_found", "User not found."));
        }

        var code = _codes.GenerateNumericCode();
        var now = _clock.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_options.TtlMinutesForLinking);

        var entity = new VerificationCode(
            userId: request.UserId,
            purpose: VerificationPurpose.LoginViaBotCode,
            targetIdentifier: request.UserId.ToString(),
            codeHash: _codes.Hash(code),
            expiresAt: expiresAt);

        _db.VerificationCodes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<string>.Success(code);
    }
}
