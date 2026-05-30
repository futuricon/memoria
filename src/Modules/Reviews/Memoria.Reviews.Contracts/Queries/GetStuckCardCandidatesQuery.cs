using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reviews.Contracts.Queries;

/// <summary>
/// Returns cards whose <paramref name="MinConsecutiveForgot"/> most recent
/// reviews are all <c>Forgot</c> — the user keeps blanking on them. The API
/// layer applies the "low stage" filter (via Reminders) and joins with
/// Cards for titles before returning to the SPA dashboard.
/// </summary>
public sealed record GetStuckCardCandidatesQuery(
    Guid UserId,
    int MinConsecutiveForgot = 3,
    int Take = 20) : IRequest<Result<IReadOnlyList<StuckCardCandidateDto>>>;
