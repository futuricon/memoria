using MediatR;

using Memoria.AI.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.AI.Contracts.Queries;

/// <summary>
/// Daily spend buckets across all users in the trailing
/// <paramref name="DaysBack"/> days, split by provider + operation so the
/// dashboard can stack Claude / DeepSeek and grading / validation.
/// </summary>
public sealed record GetAiSpendTrendQuery(int DaysBack = 30)
    : IRequest<Result<IReadOnlyList<AiSpendTrendPointDto>>>;
