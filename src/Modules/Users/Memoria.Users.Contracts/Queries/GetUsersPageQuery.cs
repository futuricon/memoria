using MediatR;

using Memoria.Shared.Kernel.Pagination;
using Memoria.Shared.Kernel.Results;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Users.Contracts.Queries;

/// <summary>
/// Admin-only paged list of users. <paramref name="Search"/> matches
/// display name and email substrings (case-insensitive). Pagination /
/// sort defaults are applied in the handler.
/// </summary>
public sealed record GetUsersPageQuery(
    int Page,
    int PageSize,
    string? Search,
    UserSortKey? Sort)
    : IRequest<Result<PagedResult<AdminUserSummaryDto>>>;
