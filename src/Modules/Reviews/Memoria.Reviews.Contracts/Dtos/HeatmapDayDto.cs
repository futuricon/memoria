namespace Memoria.Reviews.Contracts.Dtos;

/// <summary>
/// Single cell of the dashboard activity heatmap — review count for one
/// UTC date. Days with zero reviews are omitted; the SPA renders missing
/// dates as empty squares.
/// </summary>
public sealed record HeatmapDayDto(DateOnly DateUtc, int Count);
