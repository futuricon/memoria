namespace Memoria.Reviews.Contracts.Dtos;

/// <summary>
/// Spaced-repetition streak — consecutive UTC days with at least one review.
/// </summary>
/// <param name="Current">
/// Days in the current streak. Counts days strictly back from today; if today
/// has no reviews yet, the streak is still considered alive as long as
/// yesterday had at least one (grace day so users don't lose long streaks
/// before they've had coffee). Zero if no reviews exist.
/// </param>
/// <param name="Longest">Longest run of consecutive days in the user's history.</param>
/// <param name="LastReviewedOnUtc">Most recent review date, or null if none.</param>
public sealed record StreakDto(
    int Current,
    int Longest,
    DateOnly? LastReviewedOnUtc);
