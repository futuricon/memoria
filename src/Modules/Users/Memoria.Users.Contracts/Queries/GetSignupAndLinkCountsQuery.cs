using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Contracts.Queries;

public sealed record GetSignupAndLinkCountsQuery
    : IRequest<Result<SignupAndLinkCountsDto>>;
