namespace Memoria.Bot.Conversations;

public sealed record AddCardDialogState(
    AddCardStep Step,
    string? Title = null,
    string? Body = null,
    IReadOnlyList<string>? Tags = null) : ConversationState(DialogName: "AddCard");
