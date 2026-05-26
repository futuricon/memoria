using Telegram.Bot.Types;

namespace Memoria.Bot.Routing;

public interface ICallbackHandler
{
    /// <summary>
    /// Префикс <see cref="CallbackQuery.Data"/>, за который отвечает handler
    /// (например, <c>"rem:"</c>, <c>"del:"</c>, <c>"list:"</c>).
    /// </summary>
    string Prefix { get; }

    Task HandleAsync(CallbackQuery callback, CancellationToken ct);
}
