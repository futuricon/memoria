using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;
using Memoria.Users.Domain;
using Memoria.Users.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Users.Features.Stats;

internal sealed class GetSignupAndLinkCountsQueryHandler
    : IRequestHandler<GetSignupAndLinkCountsQuery, Result<SignupAndLinkCountsDto>>
{
    private readonly UsersDbContext _db;

    public GetSignupAndLinkCountsQueryHandler(UsersDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<SignupAndLinkCountsDto>> Handle(
        GetSignupAndLinkCountsQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Soft-deleted users excluded via the default query filter. Total
        // signups in the funnel = currently-present accounts; merged or
        // closed accounts are already invisible here.
        var totalSignups = await _db.Users
            .CountAsync(ct)
            .ConfigureAwait(false);

        var telegramLinked = await _db.Identities
            .Where(i => i.Provider == IdentityProvider.Telegram)
            .Select(i => i.UserId)
            .Distinct()
            .CountAsync(ct)
            .ConfigureAwait(false);

        return Result<SignupAndLinkCountsDto>.Success(
            new SignupAndLinkCountsDto(totalSignups, telegramLinked));
    }
}
