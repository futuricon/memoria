using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Contracts.Commands;

/// <summary>
/// SPA отдаёт 6-значный код, полученный пользователем в боте через
/// <c>/login</c>. При успехе возвращает JWT-пару, делает code consumed.
/// </summary>
/// <param name="Code">Plain-text 6-значный код.</param>
public sealed record ExchangeBotLoginCodeCommand(string Code) : IRequest<Result<JwtTokenPairDto>>;
