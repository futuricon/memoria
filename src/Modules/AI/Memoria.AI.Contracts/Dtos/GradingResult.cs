namespace Memoria.AI.Contracts.Dtos;

/// <summary>
/// Outcome of grading an answer. <see cref="Score"/> is 0–100;
/// <see cref="Verdict"/> is a coarse label; <see cref="Feedback"/> is a concise
/// explanation for the user.
/// </summary>
public sealed record GradingResult(
    int Score,
    GradingVerdict Verdict,
    string Feedback);
