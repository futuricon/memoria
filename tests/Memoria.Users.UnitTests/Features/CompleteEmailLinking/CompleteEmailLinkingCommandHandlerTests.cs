using FluentAssertions;

using Memoria.Shared.Infrastructure.Options;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Domain;
using Memoria.Users.Features.CompleteEmailLinking;
using Memoria.Users.Options;
using Memoria.Users.Persistence;
using Memoria.Users.Services;
using Memoria.Users.UnitTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Memoria.Users.UnitTests.Features.CompleteEmailLinking;

public sealed class CompleteEmailLinkingCommandHandlerTests
{
    private const string Email = "alice@example.com";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 5, 30, 12, 0, 0, TimeSpan.Zero));
    private readonly VerificationCodeService _codes = new();

    private readonly JwtOptions _jwtOptions = new()
    {
        Issuer = "memoria",
        Audience = "memoria-api",
        SigningKey = "memoria-test-signing-key-must-be-at-least-32-bytes-long-XYZ",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 30,
    };

    private readonly VerificationCodeOptions _codeOptions = new()
    {
        TtlMinutesForEmail = 10,
        MaxAttempts = 5,
    };

    private CompleteEmailLinkingCommandHandler CreateSut(UsersDbContext db) => new(
        db,
        _codes,
        new JwtTokenIssuer(Microsoft.Extensions.Options.Options.Create(_jwtOptions), _clock),
        _clock,
        Microsoft.Extensions.Options.Options.Create(_codeOptions));

    private VerificationCode IssueCode(UsersDbContext db, string plain, Guid? userId = null)
    {
        var entity = new VerificationCode(
            userId: userId,
            purpose: VerificationPurpose.LinkEmail,
            targetIdentifier: Email,
            codeHash: _codes.Hash(plain),
            expiresAt: _clock.GetUtcNow().UtcDateTime.AddMinutes(10));
        db.VerificationCodes.Add(entity);
        return entity;
    }

    [Fact]
    public async Task HandleWhenNoUserExistsRegistersNewUserAndIdentity()
    {
        await using var db = UsersDbContextTestFactory.Create();
        IssueCode(db, "123456");
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new CompleteEmailLinkingCommand(Email, "123456"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var user = await db.Users.SingleAsync();
        user.Email.Should().Be(Email);
        (await db.Identities.AnyAsync(i =>
            i.Provider == IdentityProvider.Email && i.ExternalId == Email)).Should().BeTrue();
    }

    [Fact]
    public async Task HandleWhenIdentityAlreadyExistsLogsInExistingUser()
    {
        // Regression: SPA email-login used to crash with a duplicate-key error
        // because the handler unconditionally tried to insert a new identity.
        await using var db = UsersDbContextTestFactory.Create();
        var existingUser = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        existingUser.SetEmail(Email);
        db.Users.Add(existingUser);
        db.Identities.Add(new UserIdentity(
            existingUser.Id, IdentityProvider.Email, Email, _clock.GetUtcNow().UtcDateTime));
        IssueCode(db, "123456");
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new CompleteEmailLinkingCommand(Email, "123456"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.Users.CountAsync()).Should().Be(1, "no new user should be created");
        (await db.Identities.CountAsync(i => i.Provider == IdentityProvider.Email))
            .Should().Be(1, "no duplicate identity should be inserted");
    }

    [Fact]
    public async Task HandleWhenUserExistsByEmailButNoIdentityAttachesIdentity()
    {
        // Pre-Telegram-linked user whose email was set via a different path
        // (manual seed, bot /link email, etc.) without an Email-provider row.
        await using var db = UsersDbContextTestFactory.Create();
        var existingUser = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        existingUser.SetEmail(Email);
        db.Users.Add(existingUser);
        IssueCode(db, "123456");
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new CompleteEmailLinkingCommand(Email, "123456"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.Users.CountAsync()).Should().Be(1, "no new user should be created");
        var identity = await db.Identities.SingleAsync(i => i.Provider == IdentityProvider.Email);
        identity.UserId.Should().Be(existingUser.Id);
    }

    [Fact]
    public async Task HandleWithInvalidCodeReturnsValidationError()
    {
        await using var db = UsersDbContextTestFactory.Create();
        IssueCode(db, "123456");
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new CompleteEmailLinkingCommand(Email, "999999"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }
}
