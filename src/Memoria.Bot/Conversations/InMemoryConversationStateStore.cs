using System.Collections.Concurrent;

namespace Memoria.Bot.Conversations;

internal sealed class InMemoryConversationStateStore : IConversationStateStore
{
    private readonly ConcurrentDictionary<long, ConversationState> _states = new();

    public bool TryGet(long chatId, out ConversationState? state)
    {
        if (_states.TryGetValue(chatId, out var found))
        {
            state = found;
            return true;
        }

        state = null;
        return false;
    }

    public void Start(long chatId, ConversationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _states[chatId] = state;
    }

    public void Update(long chatId, ConversationState updatedState)
    {
        ArgumentNullException.ThrowIfNull(updatedState);
        _states[chatId] = updatedState;
    }

    public bool Clear(long chatId) => _states.TryRemove(chatId, out _);
}
