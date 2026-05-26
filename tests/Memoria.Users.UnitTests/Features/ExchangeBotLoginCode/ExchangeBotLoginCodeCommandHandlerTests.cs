using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Domain;
using Memoria.Shared.Infrastructure.Options;
using Memoria.Users.Features.ExchangeBotLoginCode;
using Memoria.Users.Options;
using Memoria.Users.Persistence;
using Memoria.Users.Services;
using Memoria.Users.UnitTests.Infrastructure;

namespace Memoria.Users.UnitTests.Features.ExchangeBotLoginCode;

public sealed class ExchangeBotLoginCodeCommandHandlerTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero));
    private readonly VerificationCodeService _codes = new();
    private readonly VerificationCodeOptions _options = new() { MaxAttempts = 5 };

    private readonly JwtTokenIssuer _jwt = new(
        Microsoft.Extensions.Options.Options.Create(new JwtOptions
        {
            Issuer = "memoria",
            Audience = "memoria-api",
            SigningKey = "memoria-test-signing-key-must-be-at-least-32-bytes-long-XYZ",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 30,
        }),
        new FakeTimeProvider(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task HandleWithCorrectCodeReturnsJwtPairAndConsumesVerification()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var user = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        db.Users.Add(user);

        var plainCode = "123456";
        var verification = new VerificationCode(
            userId: user.Id,
            purpose: VerificationPurpose.LoginViaBotCode,
            targetIdentifier: user.Id.ToString(),
            codeHash: _codes.Hash(plainCode),
            expiresAt: _clock.GetUtcNow().UtcDateTime.AddMinutes(5));
        db.VerificationCodes.Add(verification);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var result = await sut.Handle(new ExchangeBotLoginCodeCommand(plainCode), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().NotBeNullOrEmpty();
        result.Value.RefreshToken.Should().NotBeNullOrEmpty();

        var consumed = await db.VerificationCodes.FirstAsync(c => c.Id == verification.Id);
        consumed.ConsumedAt.Should().NotBeNull();

        (await db.RefreshTokens.AnyAsync(t => t.UserId == user.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task HandleWithWrongCodeIncrementsAttemptCountAndReturnsValidation()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var user = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        db.Users.Add(user);

        var verification = new VerificationCode(
            userId: user.Id,
            purpose: VerificationPurpose.LoginViaBotCode,
            targetIdentifier: user.Id.ToString(),
            codeHash: _codes.Hash("123456"),
            expiresAt: _clock.GetUtcNow().UtcDateTime.AddMinutes(5));
        db.VerificationCodes.Add(verification);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var result = await sut.Handle(new ExchangeBotLoginCodeCommand("999999"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);

        var updated = await db.VerificationCodes.FirstAsync(c => c.Id == verification.Id);
        updated.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAfterMaxAttemptsConsumesCodeAndBlocksSubsequentAttempts()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var user = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        db.Users.Add(user);

        var verification = new VerificationCode(
            userId: user.Id,
            purpose: VerificationPurpose.LoginViaBotCode,
            targetIdentifier: user.Id.ToString(),
            codeHash: _codes.Hash("123456"),
            expiresAt: _clock.GetUtcNow().UtcDateTime.AddMinutes(5));
        db.VerificationCodes.Add(verification);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        for (var i = 0; i < _options.MaxAttempts; i++)
        {
            await sut.Handle(new ExchangeBotLoginCodeCommand("000000"), CancellationToken.None);
        }

        var consumed = await db.VerificationCodes.FirstAsync(c => c.Id == verification.Id);
        consumed.AttemptCount.Should().BeGreaterOrEqualTo(_options.MaxAttempts);
        consumed.ConsumedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleWithExpiredCodeReturnsValidation()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var user = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        db.Users.Add(user);

        var verification = new VerificationCode(
            userId: user.Id,
            purpose: VerificationPurpose.LoginViaBotCode,
            targetIdentifier: user.Id.ToString(),
            codeHash: _codes.Hash("123456"),
            expiresAt: _clock.GetUtcNow().UtcDateTime.AddMinutes(-1));
        db.VerificationCodes.Add(verification);
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var result = await sut.Handle(new ExchangeBotLoginCodeCommand("123456"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    private ExchangeBotLoginCodeCommandHandler CreateSut(UsersDbContext db) =>
        new(db, _codes, _jwt, _clock, Microsoft.Extensions.Options.Options.Create(_options));
}
