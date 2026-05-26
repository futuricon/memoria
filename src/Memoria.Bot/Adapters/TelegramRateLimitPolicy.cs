using Microsoft.Extensions.Logging;

using Polly;

using Telegram.Bot.Exceptions;

namespace Memoria.Bot.Adapters;

/// <summary>
/// Polly-policy для обработки HTTP 429 (rate limit) от Telegram. Berings
/// up to 3 retries: первая задержка — из <c>retry_after</c> (если Telegram
/// его прислал), иначе экспоненциальный fallback. Должен оборачивать
/// единичный send-call, а не весь pipeline.
/// </summary>
internal static class TelegramRateLimitPolicy
{
    public static IAsyncPolicy Build(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);

        return Policy
            .Handle<ApiRequestException>(ex => ex.ErrorCode == 429)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: (attempt, ex, _) =>
                {
                    var retryAfter = (ex as ApiRequestException)?.Parameters?.RetryAfter;
                    return retryAfter.HasValue
                        ? TimeSpan.FromSeconds(retryAfter.Value)
                        : TimeSpan.FromSeconds(Math.Pow(2, attempt));
                },
                onRetryAsync: (ex, ts, attempt, _) =>
                {
                    logger.LogWarning(
                        "Telegram 429, retrying in {Delay}s (attempt {Attempt})",
                        ts.TotalSeconds, attempt);
                    return Task.CompletedTask;
                });
    }
}
