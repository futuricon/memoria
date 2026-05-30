using FluentAssertions;

using Memoria.Shared.Infrastructure.Options;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Domain;
using Memoria.Users.Features.AuthenticateOAuth;
using Memoria.Users.Services;
using Memoria.Users.UnitTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Memoria.Users.UnitTests.Features.AuthenticateOAuth;

public sealed class AuthenticateOAuthCommandHandlerTests
{
    private const string GoogleSub = "google-sub-123";
    private const string Email = "alice@example.com";

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 5, 31, 12, 0, 0, TimeSpan.Zero));

    private readonly JwtOptions _jwtOptions = new()
    {
        Issuer = "memoria",
        Audience = "memoria-api",
        SigningKey = "memoria-test-signing-key-must-be-at-least-32-bytes-long-XYZ",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 30,
    };

    private AuthenticateOAuthCommandHandler CreateSut(Persistence.UsersDbContext db) => new(
        db,
        new JwtTokenIssuer(Microsoft.Extensions.Options.Options.Create(_jwtOptions), _clock),
        _clock);

    [Fact]
    public async Task HandleWithNewProviderAndNoMatchingEmailRegistersUser()
    {
        await using var db = UsersDbContextTestFactory.Create();

        var result = await CreateSut(db).Handle(
            new AuthenticateOAuthCommand("Google", GoogleSub, Email, EmailVerified: true, "Alice"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var user = await db.Users.SingleAsync();
        user.Email.Should().Be(Email);
        user.DisplayName.Should().Be("Alice");

        var identity = await db.Identities.SingleAsync();
        identity.Provider.Should().Be(IdentityProvider.Google);
        identity.ExternalId.Should().Be(GoogleSub);
    }

    [Fact]
    public async Task HandleWithExistingIdentityLogsInExistingUser()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var existing = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        existing.SetEmail(Email);
        db.Users.Add(existing);
        db.Identities.Add(new UserIdentity(
            existing.Id, IdentityProvider.Google, GoogleSub, _clock.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new AuthenticateOAuthCommand("Google", GoogleSub, Email, EmailVerified: true, "Alice"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.Users.CountAsync()).Should().Be(1);
        (await db.Identities.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task HandleWithVerifiedEmailMatchAttachesNewIdentityToExistingUser()
    {
        // Regression: a user who registered via email-OTP should be able to
        // sign in with Google using the same address and end up in the same
        // account, not a duplicate.
        await using var db = UsersDbContextTestFactory.Create();
        var existing = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        existing.SetEmail(Email);
        db.Users.Add(existing);
        db.Identities.Add(new UserIdentity(
            existing.Id, IdentityProvider.Email, Email, _clock.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new AuthenticateOAuthCommand("Google", GoogleSub, Email, EmailVerified: true, "Alice"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.Users.CountAsync()).Should().Be(1, "no duplicate user");
        (await db.Identities.CountAsync(i => i.Provider == IdentityProvider.Google))
            .Should().Be(1, "Google identity attached to the existing user");
    }

    [Fact]
    public async Task HandleWithUnverifiedEmailDoesNotLinkAcrossProviders()
    {
        // Security: linking by unverified email lets an attacker who can
        // spoof / pre-claim the address on a provider that doesn't verify
        // hijack an existing account.
        await using var db = UsersDbContextTestFactory.Create();
        var existing = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        existing.SetEmail(Email);
        db.Users.Add(existing);
        await db.SaveChangesAsync();

        var result = await CreateSut(db).Handle(
            new AuthenticateOAuthCommand("Google", GoogleSub, Email, EmailVerified: false, "Alice"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await db.Users.CountAsync()).Should().Be(2, "fresh user is created");
    }

    [Fact]
    public async Task HandleWithUnknownProviderReturnsValidationError()
    {
        await using var db = UsersDbContextTestFactory.Create();

        var result = await CreateSut(db).Handle(
            new AuthenticateOAuthCommand("Facebook", "fb-1", null, false, "Eve"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("users.oauth_provider_unknown");
    }

    [Fact]
    public async Task HandleWithEmailOrTelegramProviderIsRejected()
    {
        // Those providers have their own flows; routing them through here
        // would bypass the OTP / HMAC validators.
        await using var db = UsersDbContextTestFactory.Create();

        var emailRes = await CreateSut(db).Handle(
            new AuthenticateOAuthCommand("Email", Email, null, false, "Alice"),
            CancellationToken.None);
        var telegramRes = await CreateSut(db).Handle(
            new AuthenticateOAuthCommand("Telegram", "tg-1", null, false, "Alice"),
            CancellationToken.None);

        emailRes.IsFailure.Should().BeTrue();
        telegramRes.IsFailure.Should().BeTrue();
    }
}
