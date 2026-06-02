using Memoria.AI.Contracts.Dtos;
using Memoria.Users.Contracts.Dtos;

namespace Memoria.Api.Endpoints.Admin;

/// <summary>
/// API-edge composition of <see cref="AdminUserSummaryDto"/> + the user's
/// lifetime AI usage. Lives in the API layer because joining cross-module
/// data is an Api-layer concern — modules don't reference each other.
/// </summary>
public sealed record AdminUserRowDto(
    Guid Id,
    string DisplayName,
    string? Email,
    Role Role,
    DateTime CreatedAt,
    DateTime? LastSeenAt,
    bool IsBlocked,
    DateTime? DeletedAt,
    IReadOnlyList<AdminUserIdentityDto> Identities,
    long TotalInputTokens,
    long TotalOutputTokens,
    decimal EstimatedCostUsd,
    DateTime? LastCallAt,
    int CallCount);

/// <summary>
/// Page envelope tailored to the admin list — keeps the shape the SPA
/// expects without leaking <c>Shared.Kernel.Pagination.PagedResult&lt;T&gt;</c>
/// generics through OpenAPI.
/// </summary>
public sealed record AdminUserPageDto(
    IReadOnlyList<AdminUserRowDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

