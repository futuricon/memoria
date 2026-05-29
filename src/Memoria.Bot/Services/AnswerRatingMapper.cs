using Memoria.Reviews.Contracts.Dtos;

namespace Memoria.Bot.Services;

/// <summary>
/// Maps an AI grade (0–100) to a spaced-repetition <see cref="Rating"/> that
/// feeds the adaptive scheduler: ≥85 Easy, ≥65 Good, ≥40 Hard, otherwise Forgot.
/// </summary>
internal static class AnswerRatingMapper
{
    public static Rating FromScore(int score) =>
        score >= 85 ? Rating.Easy
        : score >= 65 ? Rating.Good
        : score >= 40 ? Rating.Hard
        : Rating.Forgot;
}
