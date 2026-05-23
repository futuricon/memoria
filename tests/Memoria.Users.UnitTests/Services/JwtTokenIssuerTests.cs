using System.IdentityModel.Tokens.Jwt;

using FluentAssertions;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Memoria.Users.Domain;
using Memoria.Users.Options;
using Memoria.Users.Services;

namespace Memoria.Users.UnitTests.Services;

public sealed class JwtTokenIssuerTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero));

    private readonly JwtOptions _options = new()
    {
        Issuer = "memoria",
        Audience = "memoria-api",
        SigningKey = "memoria-test-signing-key-must-be-at-least-32-bytes-long-XYZ",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 30,
    };

    private JwtTokenIssuer CreateSut() => new(Microsoft.Extensions.Options.Options.Create(_options), _clock);

    [Fact]
    public void IssueReturnsAccessTokenWithSubNameAndEmailClaims()
    {
        var user = CreateUser(email: "alice@example.com");
        var sut = CreateSut();

        var pair = sut.Issue(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(pair.AccessToken);

        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Name && c.Value == user.DisplayName);
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "alice@example.com");
    }

    [Fact]
    public void IssueOmitsEmailClaimWhenUserHasNoEmail()
    {
        var user = CreateUser(email: null);
        var sut = CreateSut();

        var pair = sut.Issue(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(pair.AccessToken);

        token.Claims.Should().NotContain(c => c.Type == JwtRegisteredClaimNames.Email);
    }

    [Fact]
    public void IssueExpiryMatchesOptions()
    {
        var sut = CreateSut();

        var pair = sut.Issue(CreateUser());

        var now = _clock.GetUtcNow().UtcDateTime;
        pair.AccessExpiresAt.Should().BeCloseTo(now.AddMinutes(15), TimeSpan.FromSeconds(1));
        pair.RefreshExpiresAt.Should().BeCloseTo(now.AddDays(30), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ValidateAccessTokenReturnsUserIdForFreshlyIssuedToken()
    {
        var user = CreateUser();
        var sut = CreateSut();
        var pair = sut.Issue(user);

        var result = sut.ValidateAccessToken(pair.AccessToken);

        result.IsSuccess.Should().BeTrue($"validation should succeed, but got: {result.Error?.Code} / {result.Error?.Message}");
        result.Value.Should().Be(user.Id);
    }

    [Fact]
    public void ValidateAccessTokenReturnsFailureForExpiredToken()
    {
        var user = CreateUser();
        var sut = CreateSut();
        var pair = sut.Issue(user);

        _clock.Advance(TimeSpan.FromMinutes(16));

        var result = sut.ValidateAccessToken(pair.AccessToken);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(Memoria.Shared.Kernel.Results.ErrorType.Unauthorized);
    }

    [Fact]
    public void ValidateAccessTokenReturnsFailureForGarbageInput()
    {
        var sut = CreateSut();

        var result = sut.ValidateAccessToken("not.a.jwt");

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void HashRefreshTokenIsDeterministic()
    {
        var sut = CreateSut();

        var hash1 = sut.HashRefreshToken("some-plain-refresh-token");
        var hash2 = sut.HashRefreshToken("some-plain-refresh-token");

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64); // sha256 hex
    }

    [Fact]
    public void HashRefreshTokenDifferentInputsYieldDifferentHashes()
    {
        var sut = CreateSut();

        sut.HashRefreshToken("a").Should().NotBe(sut.HashRefreshToken("b"));
    }

    private static User CreateUser(string? email = null)
    {
        var user = new User("Alice", "UTC", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        if (email is not null)
        {
            user.SetEmail(email);
        }
        return user;
    }
}
