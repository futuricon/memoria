using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Commands;
using Memoria.Users.Persistence;

using Microsoft.EntityFrameworkCore;

using Unit = Memoria.Shared.Kernel.Results.Unit;

namespace Memoria.Users.Features.UpdateUserPreferences;

internal sealed class UpdateUserPreferencesCommandHandler
    : IRequestHandler<UpdateUserPreferencesCommand, Result<Unit>>
{
    private readonly UsersDbContext _db;

    public UpdateUserPreferencesCommandHandler(UsersDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<Unit>> Handle(UpdateUserPreferencesCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, ct)
            .ConfigureAwait(false);

        if (user is null)
        {
            return Result<Unit>.Failure(Error.NotFound("users.not_found", "User not found."));
        }

        user.UpdatePreferences(request.TimeZoneId, request.QuietHoursStart, request.QuietHoursEnd);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return Result<Unit>.Success(Unit.Value);
    }
}
