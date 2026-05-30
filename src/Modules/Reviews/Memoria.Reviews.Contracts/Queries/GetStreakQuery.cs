using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reviews.Contracts.Queries;

/// <summary>
/// Returns the user's review streak — current consecutive-day run plus the
/// longest streak they have ever held. Operates in UTC for v1; per-user
/// timezone normalisation is a later refinement.
/// </summary>
public sealed record GetStreakQuery(Guid UserId) : IRequest<Result<StreakDto>>;
