using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reviews.Contracts.Queries;

public sealed record GetAiCalibrationQuery(int DaysBack = 90)
    : IRequest<Result<IReadOnlyList<AiCalibrationBucketDto>>>;
