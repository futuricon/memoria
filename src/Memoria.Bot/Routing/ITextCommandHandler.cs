using Telegram.Bot.Types;

namespace Memoria.Bot.Routing;

public interface ITextCommandHandler
{
    /// <summary>Имя команды без слэша, в нижнем регистре: "start", "help", "add".</summary>
    string CommandName { get; }

    /// <summary>
    /// <paramref name="payload"/> — текст после имени команды (может быть
    /// многострочным для <c>/add</c>); <c>null</c>, если ничего не передано.
    /// </summary>
    Task HandleAsync(Message message, string? payload, CancellationToken ct);
}
