namespace Memoria.Users.Contracts.Abstractions;

/// <summary>
/// Port для отправки email-сообщений. На Stage 5 реализован как заглушка
/// <c>LoggingEmailSender</c>, пишущая в Serilog. Реальный SMTP-адаптер появится позже.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Отправляет 6-значный код подтверждения на указанный email.
    /// </summary>
    Task SendVerificationCodeAsync(string email, string code, CancellationToken ct);
}
