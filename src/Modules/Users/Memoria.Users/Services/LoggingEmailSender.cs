using Microsoft.Extensions.Logging;

using Memoria.Users.Contracts.Abstractions;

namespace Memoria.Users.Services;

/// <summary>
/// Заглушка <see cref="IEmailSender"/>: вместо отправки SMTP пишет
/// код в лог. На production-итерации заменим на реальный SMTP-адаптер.
/// </summary>
internal sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public Task SendVerificationCodeAsync(string email, string code, CancellationToken ct)
    {
        _logger.LogInformation(
            "[EmailStub] Verification code for {Email}: {Code}",
            email,
            code);
        return Task.CompletedTask;
    }
}
