using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Contracts.Commands;

/// <summary>
/// Обменивает refresh-токен на новую JWT-пару. Старый refresh отзывается
/// (rotation), новый связывается через <c>ReplacedByTokenId</c>.
/// </summary>
/// <param name="RefreshToken">Plain-text refresh-токен.</param>
public sealed record RefreshAccessTokenCommand(string RefreshToken) : IRequest<Result<JwtTokenPairDto>>;
