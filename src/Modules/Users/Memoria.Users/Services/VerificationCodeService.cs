using System.Globalization;
using System.Security.Cryptography;

namespace Memoria.Users.Services;

/// <summary>
/// Чисто-функциональный сервис для работы с одноразовыми кодами:
/// генерация (цифровой код / linking-токен), хеширование (BCrypt), верификация.
/// Не делает обращений к БД.
/// </summary>
internal sealed class VerificationCodeService
{
    private const int BcryptWorkFactor = 10;

    /// <summary>
    /// Генерирует криптостойкий числовой код заданной длины (по умолчанию 6 цифр).
    /// </summary>
    public string GenerateNumericCode(int length = 6)
    {
        if (length is < 1 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be between 1 and 9 digits.");
        }

        var max = (int)Math.Pow(10, length);
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString(CultureInfo.InvariantCulture).PadLeft(length, '0');
    }

    /// <summary>
    /// Генерирует одноразовый токен для linking-flow (32 hex-символа без дефисов).
    /// </summary>
    public string GenerateLinkingToken() => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Хеширует код через BCrypt (work factor 10).
    /// </summary>
    public string Hash(string plainCode)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainCode);
        return BCrypt.Net.BCrypt.HashPassword(plainCode, BcryptWorkFactor);
    }

    /// <summary>
    /// Проверяет соответствие plain-кода и хеша. Возвращает <c>false</c> на любое расхождение.
    /// </summary>
    public bool Verify(string plainCode, string hash)
    {
        if (string.IsNullOrEmpty(plainCode) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(plainCode, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }
}
