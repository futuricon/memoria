using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Domain;
using Memoria.Users.Options;

namespace Memoria.Users.Services;

/// <summary>
/// Выдача и валидация JWT-токенов (access + refresh).
/// Access — HS256, claims: sub=UserId, name, email (опционально).
/// Refresh — 32 байта случайных данных в base64url; в БД хранится только sha256-хеш.
/// </summary>
internal sealed class JwtTokenIssuer
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParameters;

    public JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        _options = options.Value;
        _clock = clock;

        var keyBytes = Encoding.UTF8.GetBytes(_options.SigningKey);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        _signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = _clock.GetUtcNow().UtcDateTime;
                if (notBefore.HasValue && DateTime.SpecifyKind(notBefore.Value, DateTimeKind.Utc) > now)
                {
                    return false;
                }
                if (expires.HasValue && DateTime.SpecifyKind(expires.Value, DateTimeKind.Utc) <= now)
                {
                    return false;
                }
                return true;
            },
        };
    }

    /// <summary>
    /// Выдаёт пару (access, refresh) для указанного пользователя.
    /// </summary>
    public JwtTokenPair Issue(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = _clock.GetUtcNow().UtcDateTime;
        var accessExpiry = now.AddMinutes(_options.AccessTokenMinutes);
        var refreshExpiry = now.AddDays(_options.RefreshTokenDays);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: accessExpiry,
            signingCredentials: _signingCredentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);
        var refreshToken = GenerateRefreshToken();

        return new JwtTokenPair(accessToken, accessExpiry, refreshToken, refreshExpiry);
    }

    /// <summary>
    /// Возвращает sha256-хеш plain-text refresh-токена для хранения в БД.
    /// </summary>
    public string HashRefreshToken(string plainRefreshToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(plainRefreshToken);
        var bytes = Encoding.UTF8.GetBytes(plainRefreshToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Проверяет access-токен и возвращает <c>UserId</c>. Failure при невалидной подписи,
    /// просроченном токене, неверном issuer/audience.
    /// </summary>
    public Result<Guid> ValidateAccessToken(string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return Result<Guid>.Failure(Error.Validation("users.jwt_empty", "Access token is required."));
        }

        try
        {
            var handler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false,
            };
            var principal = handler.ValidateToken(accessToken, _validationParameters, out _);
            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            return Guid.TryParse(sub, out var userId)
                ? Result<Guid>.Success(userId)
                : Result<Guid>.Failure(Error.Validation("users.jwt_no_sub", "Token is missing valid 'sub' claim."));
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            return Result<Guid>.Failure(Error.Unauthorized("users.jwt_invalid", ex.Message));
        }
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncoder.Encode(bytes);
    }
}

/// <summary>
/// JWT-пара. Внутренний тип; <c>JwtTokenPairDto</c> из Contracts — это
/// внешняя проекция.
/// </summary>
internal sealed record JwtTokenPair(
    string AccessToken,
    DateTime AccessExpiresAt,
    string RefreshToken,
    DateTime RefreshExpiresAt);
