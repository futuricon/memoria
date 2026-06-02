namespace Memoria.Reviews.Contracts.Dtos;

/// <summary>
/// One AI-score bucket and how many of each <see cref="Rating"/> the user
/// actually picked when the AI score landed in that bucket. Tells the
/// admin whether the AI's score correlates with the user's self-rating
/// (i.e. the auto-grade threshold is well-tuned).
/// </summary>
public sealed record AiCalibrationBucketDto(
    int LowerInclusive,
    int UpperExclusive,
    int Forgot,
    int Hard,
    int Good,
    int Easy)
{
    public int Total => Forgot + Hard + Good + Easy;
}
