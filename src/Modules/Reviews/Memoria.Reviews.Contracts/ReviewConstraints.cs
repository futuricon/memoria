namespace Memoria.Reviews.Contracts;

/// <summary>
/// Single source of truth for review field limits. Referenced by validators, EF
/// configuration, and the bot's answer-grading flow.
/// </summary>
public static class ReviewConstraints
{
    public const int MaxAnswerLength = 2000;
}
