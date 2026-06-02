using MediatR;

using Memoria.AI.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.AI.Contracts.Queries;

/// <summary>
/// Top-N users by lifetime cost (priced via the current
/// <c>Ai:Pricing</c> table).
/// </summary>
public sealed record GetTopSpendersQuery(int Top = 10)
    : IRequest<Result<IReadOnlyList<TopSpenderDto>>>;
