using MediatR;

using Memoria.AI.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.AI.Contracts.Queries;

/// <summary>
/// Bulk look-up: lifetime usage totals for a set of user ids. Used by the
/// admin users-list endpoint, which composes a Users page with the
/// matching AI totals so the row carries spend without N+1.
/// </summary>
public sealed record GetUsersTokenTotalsQuery(IReadOnlyCollection<Guid> UserIds)
    : IRequest<Result<IReadOnlyDictionary<Guid, AiUsageTotalsDto>>>;
