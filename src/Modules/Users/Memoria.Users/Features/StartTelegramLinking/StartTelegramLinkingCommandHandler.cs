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

namespace Memoria.Users.Features.StartTelegramLinking;

internal sealed class StartTelegramLinkingCommandHandler
    : IRequestHandler<StartTelegramLinkingCommand, Result<TelegramLinkingTokenDto>>
{
    private readonly UsersDbContext _db;
    private readonly VerificationCodeService _codes;
    private readonly TimeProvider _clock;
    private readonly VerificationCodeOptions _verificationOptions;
    private readonly TelegramOptions _telegramOptions;

    public StartTelegramLinkingCommandHandler(
        UsersDbContext db,
        VerificationCodeService codes,
        TimeProvider clock,
        IOptions<VerificationCodeOptions> verificationOptions,
        IOptions<TelegramOptions> telegramOptions)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(verificationOptions);
        ArgumentNullException.ThrowIfNull(telegramOptions);

        _db = db;
        _codes = codes;
        _clock = clock;
        _verificationOptions = verificationOptions.Value;
        _telegramOptions = telegramOptions.Value;
    }

    public async Task<Result<TelegramLinkingTokenDto>> Handle(
        StartTelegramLinkingCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (user is null)
        {
            return Result<TelegramLinkingTokenDto>.Failure(
                Error.NotFound("users.not_found", "User not found."));
        }

        var token = _codes.GenerateLinkingToken();
        var now = _clock.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_verificationOptions.TtlMinutesForLinking);

        var code = new VerificationCode(
            userId: request.UserId,
            purpose: VerificationPurpose.LinkTelegram,
            targetIdentifier: token,
            codeHash: _codes.Hash(token),
            expiresAt: expiresAt);

        _db.VerificationCodes.Add(code);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var deepLink = $"https://t.me/{_telegramOptions.BotUsername}?start=link_{token}";
        return new TelegramLinkingTokenDto(token, deepLink, expiresAt);
    }
}
