using MediatR;

using Memoria.Shared.Kernel.Results;

namespace Memoria.Reviews.Contracts.Queries;

/// <summary>
/// Distinct UserId count over the Reviews module's table. Used by the
/// admin activation funnel ("first review" step) — composed at the API
/// edge with signups + cards counts.
/// </summary>
public sealed record GetUsersWithReviewCountQuery
    : IRequest<Result<int>>;
