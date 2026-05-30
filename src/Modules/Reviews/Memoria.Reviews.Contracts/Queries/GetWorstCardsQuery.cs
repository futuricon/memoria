using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reviews.Contracts.Queries;

/// <summary>
/// Returns the user's <paramref name="Take"/> cards with the lowest combined
/// (normalized) grade. Only includes cards with at least
/// <paramref name="MinReviews"/> reviews so a single bad recall doesn't dominate.
/// Combined score = COALESCE(AvgAiScore, AvgRating) ASC — same shelf because
/// both are 0–100.
/// </summary>
public sealed record GetWorstCardsQuery(Guid UserId, int Take = 5, int MinReviews = 3)
    : IRequest<Result<IReadOnlyList<CardGradeStatsDto>>>;
