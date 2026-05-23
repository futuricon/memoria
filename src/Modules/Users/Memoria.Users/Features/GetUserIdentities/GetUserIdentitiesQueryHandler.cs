using MediatR;
using Microsoft.EntityFrameworkCore;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;
using Memoria.Users.Persistence;

namespace Memoria.Users.Features.GetUserIdentities;

internal sealed class GetUserIdentitiesQueryHandler
    : IRequestHandler<GetUserIdentitiesQuery, Result<IReadOnlyList<UserIdentityDto>>>
{
    private readonly UsersDbContext _db;

    public GetUserIdentitiesQueryHandler(UsersDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<IReadOnlyList<UserIdentityDto>>> Handle(
        GetUserIdentitiesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var identities = await _db.Identities
            .Where(i => i.UserId == request.UserId)
            .OrderBy(i => i.LinkedAt)
            .Select(i => new UserIdentityDto(i.Provider.ToString(), i.ExternalId, i.LinkedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<UserIdentityDto>>.Success(identities);
    }
}
