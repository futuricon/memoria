namespace Memoria.Users.Contracts.Dtos;

/// <summary>
/// JWT-пара: access-токен короткого срока действия и refresh-токен для его обновления.
/// </summary>
/// <param name="AccessToken">Подписанный JWT, передаётся как Bearer на каждый запрос.</param>
/// <param name="AccessExpiresAt">UTC-момент истечения access-токена.</param>
/// <param name="RefreshToken">Plain-text refresh-токен. В БД хранится только его sha256-хэш.</param>
/// <param name="RefreshExpiresAt">UTC-момент истечения refresh-токена.</param>
public sealed record JwtTokenPairDto(
    string AccessToken,
    DateTime AccessExpiresAt,
    string RefreshToken,
    DateTime RefreshExpiresAt);
