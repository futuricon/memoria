using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

using Memoria.Shared.Infrastructure.Options;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Options;

namespace Memoria.Api.Authentication;

public sealed record TelegramMiniAppUser(
    long Id,
    string FirstName,
    string? LastName,
    string? Username,
    string? LanguageCode,
    string? PhotoUrl);

public sealed record TelegramMiniAppPayload(
    TelegramMiniAppUser User,
    long AuthDate,
    string? StartParam,
    string Hash);

/// <summary>
/// Валидирует <c>initData</c> Telegram Mini App. Алгоритм:
/// https://core.telegram.org/bots/webapps#validating-data-received-via-the-mini-app
/// <list type="number">
///   <item>Парсим <c>initData</c> как URL-encoded query string.</item>
///   <item>Извлекаем <c>hash</c>; остальные пары сортируем по ключу.</item>
///   <item>data-check-string = <c>key=value</c>, склеенных <c>\n</c>.</item>
///   <item>secret-key = HMAC-SHA256(key="WebAppData", data=bot_token).</item>
///   <item>expected = HMAC-SHA256(secret-key, data-check-string).</item>
///   <item>Constant-time сравнение с <c>hash</c>.</item>
///   <item>Проверяем, что <c>auth_date</c> не старше 15 минут.</item>
///   <item>Десериализуем поле <c>user</c> (URL-decoded JSON).</item>
/// </list>
/// </summary>
public sealed class TelegramMiniAppInitDataValidator
{
    private const long MaxAuthAgeSeconds = 15 * 60;

    private static readonly JsonSerializerOptions UserJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly TelegramOptions _options;
    private readonly TimeProvider _clock;

    public TelegramMiniAppInitDataValidator(IOptions<TelegramOptions> options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _options = options.Value;
        _clock = clock;
    }

    public Result<TelegramMiniAppPayload> Validate(string initData)
    {
        if (string.IsNullOrWhiteSpace(initData))
        {
            return Result<TelegramMiniAppPayload>.Failure(Error.Validation(
                "auth.miniapp_empty", "initData is empty."));
        }

        var fields = ParseQueryString(initData);

        if (!fields.TryGetValue("hash", out var providedHash) || string.IsNullOrEmpty(providedHash))
        {
            return Result<TelegramMiniAppPayload>.Failure(Error.Validation(
                "auth.miniapp_no_hash", "Missing 'hash' field."));
        }

        var dataCheckString = string.Join('\n', fields
            .Where(kvp => !string.Equals(kvp.Key, "hash", StringComparison.Ordinal))
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => $"{kvp.Key}={kvp.Value}"));

        // Mini App HMAC scheme differs from Login Widget: the secret is the HMAC
        // of the bot token under the constant key "WebAppData", NOT SHA256(token).
        var secretKey = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("WebAppData"),
            Encoding.UTF8.GetBytes(_options.BotToken));
        var expectedHashBytes = HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
        var expectedHash = Convert.ToHexString(expectedHashBytes).ToLowerInvariant();

        var expectedBytes = Encoding.ASCII.GetBytes(expectedHash);
        var providedBytes = Encoding.ASCII.GetBytes(providedHash.ToLowerInvariant());
        if (expectedBytes.Length != providedBytes.Length
            || !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            return Result<TelegramMiniAppPayload>.Failure(Error.Unauthorized(
                "auth.miniapp_bad_signature", "Telegram Mini App signature does not match."));
        }

        if (!fields.TryGetValue("auth_date", out var authDateRaw)
            || !long.TryParse(authDateRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var authDate))
        {
            return Result<TelegramMiniAppPayload>.Failure(Error.Validation(
                "auth.miniapp_no_auth_date", "Missing or malformed 'auth_date'."));
        }

        var nowUnix = _clock.GetUtcNow().ToUnixTimeSeconds();
        if (nowUnix - authDate > MaxAuthAgeSeconds)
        {
            return Result<TelegramMiniAppPayload>.Failure(Error.Unauthorized(
                "auth.miniapp_stale", "Telegram Mini App initData is older than 15 minutes."));
        }

        if (!fields.TryGetValue("user", out var userJson) || string.IsNullOrEmpty(userJson))
        {
            return Result<TelegramMiniAppPayload>.Failure(Error.Validation(
                "auth.miniapp_no_user", "Missing 'user' field."));
        }

        TelegramMiniAppUser? user;
        try
        {
            user = JsonSerializer.Deserialize<TelegramMiniAppUser>(userJson, UserJsonOptions);
        }
        catch (JsonException)
        {
            return Result<TelegramMiniAppPayload>.Failure(Error.Validation(
                "auth.miniapp_bad_user_json", "Could not parse 'user' field."));
        }

        if (user is null || user.Id == 0 || string.IsNullOrEmpty(user.FirstName))
        {
            return Result<TelegramMiniAppPayload>.Failure(Error.Validation(
                "auth.miniapp_user_incomplete", "'user' field is missing required properties."));
        }

        fields.TryGetValue("start_param", out var startParam);

        return Result<TelegramMiniAppPayload>.Success(new TelegramMiniAppPayload(
            user, authDate, startParam, providedHash));
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var sep = pair.IndexOf('=', StringComparison.Ordinal);
            if (sep <= 0) continue;
            var key = HttpUtility.UrlDecode(pair[..sep]);
            var value = HttpUtility.UrlDecode(pair[(sep + 1)..]);
            if (!string.IsNullOrEmpty(key))
            {
                result[key] = value;
            }
        }
        return result;
    }
}