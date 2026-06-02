namespace Memoria.AI.Contracts.Dtos;

/// <summary>
/// Input for grading a free-text answer to a Question card. UserId is
/// required so the grader can attribute the resulting AI spend to a
/// specific user via <c>AiUsageRecorded</c>.
/// </summary>
public sealed record GradingRequest(
    Guid UserId,
    string Question,
    string ReferenceBody,
    string UserAnswer);
