using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Memoria.Shared.Infrastructure.Options;
using Memoria.Shared.Kernel.Results;

using Microsoft.Extensions.Options;

namespace Memoria.Api.Authentication;

public sealed record TelegramWidgetPayload(
    long Id,
    string FirstName,
    string? LastName,
    string? Username,
    string? PhotoUrl,
    long AuthDate,
    string Hash);

/// <summary>
/// Валидирует payload Telegram Login Widget. Алгоритм (см.
/// https://core.telegram.org/widgets/login#checking-authorization):
/// <list type="number">
///   <item>Извлекаем <c>hash</c> из payload, остальные поля сортируем по ключу.</item>
///   <item>Строим data-check-string = <c>key=value</c>, склеенных <c>\n</c>.</item>
///   <item>secret-key = SHA256(bot_token); expected-hash = HMAC-SHA256(secret-key, data-check-string).</item>
///   <item>Сравниваем константно-временно с <c>hash</c>; mismatch → <c>Unauthorized</c>.</item>
///   <item>Проверяем, что <c>auth_date</c> не старше 24 часов.</item>
/// </list>
/// </summary>
public sealed class TelegramWidgetValidator
{
    private const long MaxAuthAgeSeconds = 24 * 60 * 60;

    private readonly TelegramOptions _options;
    private readonly TimeProvider _clock;

    public TelegramWidgetValidator(IOptions<TelegramOptions> options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        _options = options.Value;
        _clock = clock;
    }

    public Result<TelegramWidgetPayload> Validate(IDictionary<string, string> rawFields)
    {
        ArgumentNullException.ThrowIfNull(rawFields);

        if (!rawFields.TryGetValue("hash", out var providedHash) || string.IsNullOrEmpty(providedHash))
        {
            return Result<TelegramWidgetPayload>.Failure(Error.Validation(
                "auth.widget_no_hash", "Missing 'hash' field."));
        }

        var fields = rawFields
            .Where(kvp => !string.Equals(kvp.Key, "hash", StringComparison.Ordinal))
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal);

        var dataCheckString = string.Join('\n', fields.Select(kvp => $"{kvp.Key}={kvp.Value}"));

        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(_options.BotToken));
        var expectedHashBytes = HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(dataCheckString));
        var expectedHash = Convert.ToHexString(expectedHashBytes).ToLowerInvariant();

        var expectedBytes = Encoding.ASCII.GetBytes(expectedHash);
        var providedBytes = Encoding.ASCII.GetBytes(providedHash.ToLowerInvariant());
        if (expectedBytes.Length != providedBytes.Length
            || !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            return Result<TelegramWidgetPayload>.Failure(Error.Unauthorized(
                "auth.widget_bad_signature", "Telegram widget signature does not match."));
        }

        if (!rawFields.TryGetValue("auth_date", out var authDateRaw)
            || !long.TryParse(authDateRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var authDate))
        {
            return Result<TelegramWidgetPayload>.Failure(Error.Validation(
                "auth.widget_no_auth_date", "Missing or malformed 'auth_date'."));
        }

        var nowUnix = _clock.GetUtcNow().ToUnixTimeSeconds();
        if (nowUnix - authDate > MaxAuthAgeSeconds)
        {
            return Result<TelegramWidgetPayload>.Failure(Error.Unauthorized(
                "auth.widget_stale", "Telegram widget payload is older than 24 hours."));
        }

        if (!rawFields.TryGetValue("id", out var idRaw)
            || !long.TryParse(idRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            return Result<TelegramWidgetPayload>.Failure(Error.Validation(
                "auth.widget_no_id", "Missing or malformed 'id'."));
        }

        if (!rawFields.TryGetValue("first_name", out var firstName) || string.IsNullOrEmpty(firstName))
        {
            return Result<TelegramWidgetPayload>.Failure(Error.Validation(
                "auth.widget_no_first_name", "Missing 'first_name'."));
        }

        rawFields.TryGetValue("last_name", out var lastName);
        rawFields.TryGetValue("username", out var username);
        rawFields.TryGetValue("photo_url", out var photoUrl);

        return Result<TelegramWidgetPayload>.Success(new TelegramWidgetPayload(
            id, firstName, lastName, username, photoUrl, authDate, providedHash));
    }
}
