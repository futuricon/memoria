using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Contracts.Commands;

/// <summary>
/// Завершает email-привязку: проверяет код, создаёт UserIdentity (Email),
/// возвращает JWT-пару. Если в VerificationCode не было UserId — создаёт
/// нового пользователя и привязывает к нему email.
/// </summary>
/// <param name="Email">Email, на который шёл код.</param>
/// <param name="Code">Plain-text 6-значный код.</param>
public sealed record CompleteEmailLinkingCommand(string Email, string Code) : IRequest<Result<JwtTokenPairDto>>;
