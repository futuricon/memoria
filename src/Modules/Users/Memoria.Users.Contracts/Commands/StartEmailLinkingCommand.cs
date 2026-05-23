using MediatR;

using Memoria.Shared.Kernel.Results;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Users.Contracts.Commands;

/// <summary>
/// Начинает email-привязку: генерирует 6-значный код, сохраняет его в БД
/// и отправляет через <see cref="Abstractions.IEmailSender"/>. На Stage 5 sender — заглушка.
/// </summary>
/// <param name="UserId">
/// ID пользователя для привязки email. <c>null</c> означает регистрацию нового
/// пользователя через email.
/// </param>
/// <param name="Email">Email для привязки/регистрации.</param>
public sealed record StartEmailLinkingCommand(Guid? UserId, string Email) : IRequest<Result<Unit>>;
