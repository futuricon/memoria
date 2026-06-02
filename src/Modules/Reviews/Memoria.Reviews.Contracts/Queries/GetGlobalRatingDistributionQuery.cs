using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reviews.Contracts.Queries;

/// <summary>
/// Rating histogram across <em>every</em> user's reviews in the trailing
/// window. The per-user version lives in <see cref="GetRatingDistributionQuery"/>.
/// </summary>
public sealed record GetGlobalRatingDistributionQuery(int DaysBack = 30)
    : IRequest<Result<RatingDistributionDto>>;
