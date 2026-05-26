using MediatR;

using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;
using Memoria.Users.Domain;
using Memoria.Users.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Users.Features.GetUserByTelegramId;

internal sealed class GetUserByTelegramIdQueryHandler
    : IRequestHandler<GetUserByTelegramIdQuery, Result<UserIdentityResolutionDto>>
{
    private readonly UsersDbContext _db;

    public GetUserByTelegramIdQueryHandler(UsersDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<UserIdentityResolutionDto>> Handle(
        GetUserByTelegramIdQuery request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dto = await (
            from identity in _db.Identities
            where identity.Provider == IdentityProvider.Telegram
                  && identity.ExternalId == request.TelegramId
            join user in _db.Users on identity.UserId equals user.Id
            select new UserIdentityResolutionDto(user.Id, user.DisplayName, user.Email)
        ).FirstOrDefaultAsync(ct).ConfigureAwait(false);

        if (dto is null)
        {
            return Result<UserIdentityResolutionDto>.Failure(
                Error.NotFound("users.telegram_not_linked",
                    "Telegram identity is not linked to any user."));
        }

        return Result<UserIdentityResolutionDto>.Success(dto);
    }
}
