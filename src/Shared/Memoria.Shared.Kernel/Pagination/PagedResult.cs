namespace Memoria.Shared.Kernel.Pagination;

/// <summary>
/// Shared paging envelope. Lives in Shared.Kernel so any module's Contracts
/// can return one without crossing module-Contracts boundaries.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
