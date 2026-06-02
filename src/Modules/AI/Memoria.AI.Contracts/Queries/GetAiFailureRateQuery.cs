using MediatR;

using Memoria.AI.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.AI.Contracts.Queries;

public sealed record GetAiFailureRateQuery(int DaysBack = 30)
    : IRequest<Result<AiFailureRateDto>>;
