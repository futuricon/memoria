namespace Memoria.AI.Contracts.Dtos;

/// <summary>
/// Classifies what an AI call was for. Persisted on every <c>ai_usage</c>
/// row so admin analytics can break spend down by operation type, and so
/// quota enforcement (Phase 5) can apply different ceilings to different
/// flows if needed.
/// </summary>
public enum AiOperation
{
    /// <summary>Grading a free-text answer to a Question card.</summary>
    AnswerGrading = 0,

    /// <summary>Validating that a new Question card's body coherently
    /// answers its title.</summary>
    QuestionCardValidation = 1,
}
