using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reviews.Contracts.Queries;

/// <summary>
/// Batches review aggregation for a list of cards. Always scoped by
/// <paramref name="UserId"/>. Cards with zero reviews are simply absent from
/// the result — callers must treat missing entries as "no stats yet".
/// </summary>
public sealed record GetCardGradeStatsQuery(
    Guid UserId,
    IReadOnlyList<Guid> CardIds) : IRequest<Result<IReadOnlyList<CardGradeStatsDto>>>;
