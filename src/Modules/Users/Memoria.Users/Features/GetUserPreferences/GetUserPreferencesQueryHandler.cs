using MediatR;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;
using Memoria.Users.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Memoria.Users.Features.GetUserPreferences;

internal sealed class GetUserPreferencesQueryHandler
    : IRequestHandler<GetUserPreferencesQuery, Result<UserPreferencesDto>>
{
    private readonly UsersDbContext _db;

    public GetUserPreferencesQueryHandler(UsersDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<UserPreferencesDto>> Handle(GetUserPreferencesQuery request, CancellationToken ct)
    {
        var user = await _db.Users
            .Where(u => u.Id == request.UserId)
            .Select(u => new UserPreferencesDto(u.Id, u.TimeZoneId, u.QuietHoursStart, u.QuietHoursEnd))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (user == null)
            return Error.NotFound("users.not_found", "User not found.");

        return user;
    }
}