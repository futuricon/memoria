namespace Memoria.Bot.Conversations;

/// <summary>
/// In-memory FSM-store для активных диалогов бота. Состояние теряется при
/// рестарте процесса — это приемлемо по брифу (FSM-диалоги короткие).
/// </summary>
public interface IConversationStateStore
{
    bool TryGet(long chatId, out ConversationState? state);
    void Start(long chatId, ConversationState state);
    void Update(long chatId, ConversationState updatedState);
    /// <summary>Возвращает <c>true</c>, если было активное состояние и оно удалено.</summary>
    bool Clear(long chatId);
}
