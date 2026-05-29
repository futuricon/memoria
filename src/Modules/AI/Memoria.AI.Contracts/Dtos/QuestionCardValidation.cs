namespace Memoria.AI.Contracts.Dtos;

/// <summary>
/// Result of checking that a Question card's body is a coherent, relevant
/// answer to its title. <see cref="Reason"/> explains why it is not coherent
/// (shown to the user) and is <c>null</c> when <see cref="IsCoherent"/> is true.
/// </summary>
public sealed record QuestionCardValidation(
    bool IsCoherent,
    string? Reason);
