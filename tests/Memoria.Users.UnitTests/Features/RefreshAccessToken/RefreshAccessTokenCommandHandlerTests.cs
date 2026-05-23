using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Domain;
using Memoria.Users.Features.RefreshAccessToken;
using Memoria.Users.Options;
using Memoria.Users.Services;
using Memoria.Users.UnitTests.Infrastructure;

namespace Memoria.Users.UnitTests.Features.RefreshAccessToken;

public sealed class RefreshAccessTokenCommandHandlerTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero));

    private readonly JwtTokenIssuer _jwt;

    public RefreshAccessTokenCommandHandlerTests()
    {
        _jwt = new JwtTokenIssuer(
            Microsoft.Extensions.Options.Options.Create(new JwtOptions
            {
                Issuer = "memoria",
                Audience = "memoria-api",
                SigningKey = "memoria-test-signing-key-must-be-at-least-32-bytes-long-XYZ",
                AccessTokenMinutes = 15,
                RefreshTokenDays = 30,
            }),
            _clock);
    }

    [Fact]
    public async Task HandleWithValidTokenIssuesNewPairAndRevokesOld()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var user = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        db.Users.Add(user);

        const string plainRefresh = "plain-refresh-token-value";
        var stored = new RefreshToken(
            userId: user.Id,
            tokenHash: _jwt.HashRefreshToken(plainRefresh),
            expiresAt: _clock.GetUtcNow().UtcDateTime.AddDays(30),
            createdAt: _clock.GetUtcNow().UtcDateTime);
        db.RefreshTokens.Add(stored);
        await db.SaveChangesAsync();

        var sut = new RefreshAccessTokenCommandHandler(db, _jwt, _clock);
        var result = await sut.Handle(new RefreshAccessTokenCommand(plainRefresh), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RefreshToken.Should().NotBe(plainRefresh);

        var revoked = await db.RefreshTokens.FirstAsync(t => t.Id == stored.Id);
        revoked.RevokedAt.Should().NotBeNull();
        revoked.ReplacedByTokenId.Should().NotBeNull();

        (await db.RefreshTokens.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task HandleWithRevokedTokenReturnsUnauthorized()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var user = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        db.Users.Add(user);

        const string plainRefresh = "plain-refresh-token";
        var stored = new RefreshToken(
            userId: user.Id,
            tokenHash: _jwt.HashRefreshToken(plainRefresh),
            expiresAt: _clock.GetUtcNow().UtcDateTime.AddDays(30),
            createdAt: _clock.GetUtcNow().UtcDateTime);
        stored.Revoke(_clock.GetUtcNow().UtcDateTime);
        db.RefreshTokens.Add(stored);
        await db.SaveChangesAsync();

        var sut = new RefreshAccessTokenCommandHandler(db, _jwt, _clock);
        var result = await sut.Handle(new RefreshAccessTokenCommand(plainRefresh), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task HandleWithExpiredTokenReturnsUnauthorized()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var user = new User("Alice", "UTC", _clock.GetUtcNow().UtcDateTime);
        db.Users.Add(user);

        const string plainRefresh = "plain-refresh-token";
        var stored = new RefreshToken(
            userId: user.Id,
            tokenHash: _jwt.HashRefreshToken(plainRefresh),
            expiresAt: _clock.GetUtcNow().UtcDateTime.AddDays(-1),
            createdAt: _clock.GetUtcNow().UtcDateTime.AddDays(-31));
        db.RefreshTokens.Add(stored);
        await db.SaveChangesAsync();

        var sut = new RefreshAccessTokenCommandHandler(db, _jwt, _clock);
        var result = await sut.Handle(new RefreshAccessTokenCommand(plainRefresh), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task HandleWithUnknownTokenReturnsUnauthorized()
    {
        await using var db = UsersDbContextTestFactory.Create();
        var sut = new RefreshAccessTokenCommandHandler(db, _jwt, _clock);

        var result = await sut.Handle(new RefreshAccessTokenCommand("totally-unknown-token"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
    }
}
