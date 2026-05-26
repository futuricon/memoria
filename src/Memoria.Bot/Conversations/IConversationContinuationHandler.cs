using Telegram.Bot.Types;

namespace Memoria.Bot.Conversations;

/// <summary>
/// Обработчик "продолжения" FSM-диалога: получает следующее текстовое
/// сообщение пользователя и текущее состояние диалога. Регистрируется по
/// <see cref="DialogName"/>, который должен совпадать со значением
/// <see cref="ConversationState.DialogName"/> у соответствующего state.
/// </summary>
public interface IConversationContinuationHandler
{
    string DialogName { get; }

    Task HandleAsync(Message message, ConversationState state, CancellationToken ct);
}
