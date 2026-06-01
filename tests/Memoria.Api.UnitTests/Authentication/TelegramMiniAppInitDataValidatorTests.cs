using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;

using FluentAssertions;

using Memoria.Api.Authentication;
using Memoria.Shared.Infrastructure.Options;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Memoria.Api.UnitTests.Authentication;

public sealed class TelegramMiniAppInitDataValidatorTests
{
    private const string BotToken = "12345:test-bot-token-deadbeef";
    private static readonly DateTime FixedNowUtc = new(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(FixedNowUtc, TimeSpan.Zero));
    private readonly TelegramMiniAppInitDataValidator _sut;

    public TelegramMiniAppInitDataValidatorTests()
    {
        var opts = Options.Create(new TelegramOptions { BotToken = BotToken, BotUsername = "memoria_bot" });
        _sut = new TelegramMiniAppInitDataValidator(opts, _clock);
    }

    private static string SignedInitData(long authDate, string userJson, string? startParam = null, string? tamperHash = null)
    {
        // Build raw fields exactly as Telegram sends them in initData (URL-decoded values).
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = authDate.ToString(CultureInfo.InvariantCulture),
            ["user"] = userJson,
        };
        if (startParam is not null) fields["start_param"] = startParam;

        var dataCheckString = string.Join('\n', fields.Select(k => $"{k.Key}={k.Value}"));
        var secretKey = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("WebAppData"),
            Encoding.UTF8.GetBytes(BotToken));
        var hashBytes = HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
        var hash = tamperHash ?? Convert.ToHexString(hashBytes).ToLowerInvariant();

        // Encode back to a query string the same way tg.initData would arrive.
        var pairs = fields.Select(kvp => $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}");
        return string.Join('&', pairs) + $"&hash={hash}";
    }

    [Fact]
    public void ValidateAcceptsCorrectlySignedInitData()
    {
        var authDate = new DateTimeOffset(FixedNowUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        var userJson = """{"id":1001,"first_name":"Ada","last_name":"Lovelace","username":"ada","language_code":"en"}""";
        var initData = SignedInitData(authDate, userJson, startParam: "link_abc");

        var result = _sut.Validate(initData);

        result.IsSuccess.Should().BeTrue();
        result.Value!.User.Id.Should().Be(1001);
        result.Value.User.FirstName.Should().Be("Ada");
        result.Value.User.LastName.Should().Be("Lovelace");
        result.Value.User.Username.Should().Be("ada");
        result.Value.User.LanguageCode.Should().Be("en");
        result.Value.StartParam.Should().Be("link_abc");
    }

    [Fact]
    public void ValidateAcceptsMinimalUserPayload()
    {
        var authDate = new DateTimeOffset(FixedNowUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        var userJson = """{"id":42,"first_name":"Bob"}""";
        var initData = SignedInitData(authDate, userJson);

        var result = _sut.Validate(initData);

        result.IsSuccess.Should().BeTrue();
        result.Value!.User.Id.Should().Be(42);
        result.Value.User.FirstName.Should().Be("Bob");
        result.Value.User.LastName.Should().BeNull();
        result.Value.User.Username.Should().BeNull();
        result.Value.StartParam.Should().BeNull();
    }

    [Fact]
    public void ValidateRejectsTamperedHash()
    {
        var authDate = new DateTimeOffset(FixedNowUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        var userJson = """{"id":1001,"first_name":"Ada"}""";
        var initData = SignedInitData(authDate, userJson,
            tamperHash: new string('0', 64));

        var result = _sut.Validate(initData);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("auth.miniapp_bad_signature");
    }

    [Fact]
    public void ValidateRejectsTamperedUserField()
    {
        var authDate = new DateTimeOffset(FixedNowUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        var userJson = """{"id":1001,"first_name":"Ada"}""";
        var initData = SignedInitData(authDate, userJson);
        // Swap user JSON to attempt impersonation while keeping the original hash.
        var tampered = initData.Replace(
            HttpUtility.UrlEncode(userJson),
            HttpUtility.UrlEncode("""{"id":9999,"first_name":"Mallory"}"""),
            StringComparison.Ordinal);

        var result = _sut.Validate(tampered);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("auth.miniapp_bad_signature");
    }

    [Fact]
    public void ValidateRejectsStaleAuthDate()
    {
        var staleAuthDate = new DateTimeOffset(FixedNowUtc, TimeSpan.Zero).ToUnixTimeSeconds() - (16 * 60);
        var userJson = """{"id":1001,"first_name":"Ada"}""";
        var initData = SignedInitData(staleAuthDate, userJson);

        var result = _sut.Validate(initData);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("auth.miniapp_stale");
    }

    [Fact]
    public void ValidateRejectsEmptyInitData()
    {
        var result = _sut.Validate("");

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("auth.miniapp_empty");
    }

    [Fact]
    public void ValidateRejectsMissingUser()
    {
        // Hand-craft initData without a user field, sign correctly, expect rejection.
        var authDate = new DateTimeOffset(FixedNowUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = authDate.ToString(CultureInfo.InvariantCulture),
        };
        var dataCheckString = string.Join('\n', fields.Select(k => $"{k.Key}={k.Value}"));
        var secretKey = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("WebAppData"),
            Encoding.UTF8.GetBytes(BotToken));
        var hash = Convert.ToHexString(HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString)))
            .ToLowerInvariant();
        var initData = $"auth_date={authDate}&hash={hash}";

        var result = _sut.Validate(initData);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("auth.miniapp_no_user");
    }
}
