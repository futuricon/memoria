namespace Memoria.Reviews.Contracts.Dtos;

/// <summary>
/// Histogram of review ratings over a recent window. Used by the SPA
/// dashboard to show "how am I doing lately" at a glance.
/// </summary>
public sealed record RatingDistributionDto(
    int Forgot,
    int Hard,
    int Good,
    int Easy)
{
    public int Total => Forgot + Hard + Good + Easy;
}
