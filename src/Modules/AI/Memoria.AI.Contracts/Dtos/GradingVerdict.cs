namespace Memoria.AI.Contracts.Dtos;

/// <summary>
/// Coarse classification of a graded answer, used for display alongside the
/// numeric score. The score (not the verdict) drives the spaced-repetition
/// rating.
/// </summary>
public enum GradingVerdict
{
    Incorrect,
    Partial,
    Correct,
}
