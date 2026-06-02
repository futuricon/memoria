using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Contracts.Queries;

/// <summary>
/// Retention for users signed up in the <paramref name="WindowDays"/>-day
/// window ending 30 days ago (so D30 has time to mature).
/// </summary>
public sealed record GetRetentionCohortsQuery(int WindowDays = 30)
    : IRequest<Result<RetentionCohortsDto>>;
