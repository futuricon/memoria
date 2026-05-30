using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reviews.Contracts.Queries;

/// <summary>
/// Counts of each <c>Rating</c> across the user's reviews in the trailing
/// <paramref name="DaysBack"/> days (default 30). Used by the dashboard
/// "how am I doing lately" histogram widget.
/// </summary>
public sealed record GetRatingDistributionQuery(Guid UserId, int DaysBack = 30)
    : IRequest<Result<RatingDistributionDto>>;
