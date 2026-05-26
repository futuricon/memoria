namespace Memoria.Reviews.Contracts.Dtos;

/// <summary>
/// Public projection of a recorded <c>Review</c>.
/// </summary>
public sealed record ReviewDto(
    Guid Id,
    Guid CardId,
    Guid UserId,
    Guid? ReminderId,
    Rating Rating,
    string CardTitleSnapshot,
    DateTime ReviewedAt,
    string? Note);
