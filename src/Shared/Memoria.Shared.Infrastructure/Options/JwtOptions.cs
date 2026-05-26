using System.ComponentModel.DataAnnotations;

namespace Memoria.Shared.Infrastructure.Options;

/// <summary>
/// Конфигурация JWT-токенов. Используется одновременно несколькими местами:
/// Memoria.Users эмитит токены через <c>JwtTokenIssuer</c>; Memoria.Api валидирует
/// входящие Bearer-токены с теми же Issuer/Audience/SigningKey.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Required]
    [MinLength(32, ErrorMessage = "HS256 signing key must be at least 32 bytes.")]
    public string SigningKey { get; init; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 15;

    [Range(1, 365)]
    public int RefreshTokenDays { get; init; } = 30;
}
