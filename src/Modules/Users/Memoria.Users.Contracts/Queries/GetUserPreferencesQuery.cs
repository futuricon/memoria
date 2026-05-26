using MediatR;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Contracts.Queries;

public sealed record GetUserPreferencesQuery(Guid UserId) 
    : IRequest<Result<UserPreferencesDto>>;