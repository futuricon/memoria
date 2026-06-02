using MediatR;

using Memoria.Shared.Kernel.Results;

namespace Memoria.Cards.Contracts.Queries;

/// <summary>
/// Distinct UserId count over the Cards module's table. Used by the
/// admin activation funnel ("first card" step) — composed at the API
/// edge with the signup count from Users.
/// </summary>
public sealed record GetUsersWithCardCountQuery
    : IRequest<Result<int>>;
