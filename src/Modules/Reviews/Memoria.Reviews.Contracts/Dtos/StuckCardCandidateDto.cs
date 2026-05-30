namespace Memoria.Reviews.Contracts.Dtos;

/// <summary>
/// Card surfaced by <c>GetStuckCardCandidatesQuery</c> because its last N
/// reviews were all <c>Forgot</c>. The API layer then filters by current
/// reminder stage and joins with the card title for display.
/// </summary>
public sealed record StuckCardCandidateDto(
    Guid CardId,
    int ConsecutiveForgotCount,
    DateTime LastReviewedAt);
