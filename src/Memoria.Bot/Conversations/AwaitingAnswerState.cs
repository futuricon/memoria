namespace Memoria.Bot.Conversations;

/// <summary>
/// Active conversation state while the bot waits for the user's free-text
/// answer to a Question-card reminder. The next text message in the chat is
/// graded against the card's reference answer.
/// </summary>
public sealed record AwaitingAnswerState(Guid ReminderId, Guid CardId)
    : ConversationState(DialogName: "AwaitingAnswer");
