namespace Memoria.AI.Contracts.Dtos;

/// <summary>
/// Input for grading a free-text answer to a Question card: the original
/// question, the reference answer (the card body), and the student's answer.
/// </summary>
public sealed record GradingRequest(
    string Question,
    string ReferenceBody,
    string UserAnswer);
