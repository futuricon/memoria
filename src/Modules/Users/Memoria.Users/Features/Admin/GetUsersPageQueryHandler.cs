using MediatR;

using Memoria.Shared.Kernel.Pagination;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;
using Memoria.Users.Contracts.Queries;
using Memoria.Users.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Memoria.Users.Features.Admin;

internal sealed class GetUsersPageQueryHandler
    : IRequestHandler<GetUsersPageQuery, Result<PagedResult<AdminUserSummaryDto>>>
{
    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;

    private readonly UsersDbContext _db;

    public GetUsersPageQueryHandler(UsersDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task<Result<PagedResult<AdminUserSummaryDto>>> Handle(
        GetUsersPageQuery request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(
            request.PageSize <= 0 ? DefaultPageSize : request.PageSize,
            1,
            MaxPageSize);

        // Admin view spans every user — including soft-deleted accounts, which
        // is the whole point of having DeletedAt on the DTO.
        var query = _db.Users.IgnoreQueryFilters().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.DisplayName, pattern)
                || (u.Email != null && EF.Functions.ILike(u.Email, pattern)));
        }

        query = (request.Sort ?? UserSortKey.CreatedAtDesc) switch
        {
            UserSortKey.CreatedAtAsc => query.OrderBy(u => u.CreatedAt),
            UserSortKey.LastSeenAtDesc => query
                .OrderByDescending(u => u.LastSeenAt == null)
                .ThenByDescending(u => u.LastSeenAt),
            UserSortKey.DisplayNameAsc => query.OrderBy(u => u.DisplayName),
            _ => query.OrderByDescending(u => u.CreatedAt),
        };

        var totalCount = await query.CountAsync(ct).ConfigureAwait(false);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserSummaryDto(
                u.Id,
                u.DisplayName,
                u.Email,
                u.Role,
                u.CreatedAt,
                u.LastSeenAt,
                u.IsBlocked,
                u.DeletedAt))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return Result<PagedResult<AdminUserSummaryDto>>.Success(
            new PagedResult<AdminUserSummaryDto>(items, page, pageSize, totalCount));
    }
}
