using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;
using Memoria.Users.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Users.Features.GetCurrentUser;

internal sealed class GetCurrentUserQueryHandler
    : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserDto>>
{
    private readonly UsersDbContext _db;

    public GetCurrentUserQueryHandler(UsersDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<CurrentUserDto>> Handle(GetCurrentUserQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dto = await _db.Users
            .Where(u => u.Id == request.UserId)
            .Select(u => new CurrentUserDto(
                u.Id,
                u.DisplayName,
                u.Email,
                u.TimeZoneId,
                u.QuietHoursStart,
                u.QuietHoursEnd,
                u.CreatedAt))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return Result<CurrentUserDto>.Failure(Error.NotFound(
                "users.not_found", "User not found."));
        }

        return Result<CurrentUserDto>.Success(dto);
    }
}
