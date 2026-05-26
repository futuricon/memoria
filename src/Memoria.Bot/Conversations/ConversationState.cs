namespace Memoria.Bot.Conversations;

/// <summary>
/// Базовый record для всех состояний многошаговых FSM-диалогов бота.
/// Конкретные диалоги (например, <c>AddCardDialogState</c>) наследуются и
/// добавляют свои поля.
/// </summary>
public abstract record ConversationState(string DialogName);
