using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using FluentAssertions;

using Memoria.Api.Authentication;
using Memoria.Shared.Infrastructure.Options;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Memoria.Api.UnitTests.Authentication;

public sealed class TelegramWidgetValidatorTests
{
    private const string BotToken = "12345:test-bot-token-deadbeef";
    private static readonly DateTime FixedNowUtc = new(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(FixedNowUtc, TimeSpan.Zero));
    private readonly TelegramWidgetValidator _sut;

    public TelegramWidgetValidatorTests()
    {
        var opts = Options.Create(new TelegramOptions { BotToken = BotToken, BotUsername = "memoria_bot" });
        _sut = new TelegramWidgetValidator(opts, _clock);
    }

    private static Dictionary<string, string> SignedPayload(long authDate, string? mutateField = null, string? mutateValue = null)
    {
        var fields = new Dictionary<string, string>
        {
            ["id"] = "1001",
            ["first_name"] = "Ada",
            ["last_name"] = "Lovelace",
            ["username"] = "ada",
            ["auth_date"] = authDate.ToString(CultureInfo.InvariantCulture),
        };
        if (mutateField is not null && mutateValue is not null)
        {
            fields[mutateField] = mutateValue;
        }

        var dataCheckString = string.Join('\n',
            fields.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => $"{k.Key}={k.Value}"));
        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(BotToken));
        var hashBytes = HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
        fields["hash"] = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return fields;
    }

    [Fact]
    public void ValidateAcceptsCorrectlySignedPayload()
    {
        var authDate = new DateTimeOffset(FixedNowUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        var payload = SignedPayload(authDate);

        var result = _sut.Validate(payload);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(1001);
        result.Value.FirstName.Should().Be("Ada");
        result.Value.LastName.Should().Be("Lovelace");
        result.Value.Username.Should().Be("ada");
    }

    [Fact]
    public void ValidateRejectsTamperedHash()
    {
        var authDate = new DateTimeOffset(FixedNowUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        var payload = SignedPayload(authDate);
        var sb = new StringBuilder(payload["hash"]);
        sb[0] = sb[0] == 'a' ? 'b' : 'a'; // flip one char
        payload["hash"] = sb.ToString();

        var result = _sut.Validate(payload);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("auth.widget_bad_signature");
    }

    [Fact]
    public void ValidateRejectsStaleAuthDate()
    {
        var staleAuthDate = new DateTimeOffset(FixedNowUtc, TimeSpan.Zero).ToUnixTimeSeconds() - (25 * 60 * 60);
        var payload = SignedPayload(staleAuthDate);

        var result = _sut.Validate(payload);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Unauthorized);
        result.Error.Code.Should().Be("auth.widget_stale");
    }

    [Fact]
    public void ValidateRejectsMissingHash()
    {
        var payload = new Dictionary<string, string>
        {
            ["id"] = "1001",
            ["first_name"] = "Ada",
            ["auth_date"] = "1717000000",
        };

        var result = _sut.Validate(payload);

        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("auth.widget_no_hash");
    }
}
