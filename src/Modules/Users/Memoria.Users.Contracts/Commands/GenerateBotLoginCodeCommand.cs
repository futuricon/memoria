using MediatR;

using Memoria.Shared.Kernel.Results;

namespace Memoria.Users.Contracts.Commands;

/// <summary>
/// Сценарий 2: пользователь в боте хочет войти в SPA. Генерирует 6-значный
/// одноразовый код, который пользователь увидит в боте и введёт в SPA.
/// </summary>
/// <param name="UserId">ID пользователя, который запросил код в боте.</param>
public sealed record GenerateBotLoginCodeCommand(Guid UserId) : IRequest<Result<string>>;
