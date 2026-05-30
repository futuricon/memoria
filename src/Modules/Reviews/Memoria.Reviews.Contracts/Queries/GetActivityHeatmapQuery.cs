using MediatR;

using Memoria.Reviews.Contracts.Dtos;
using Memoria.Shared.Kernel.Results;

namespace Memoria.Reviews.Contracts.Queries;

/// <summary>
/// Returns per-day review counts for the user across the trailing
/// <paramref name="DaysBack"/> days (default 90 — roughly one quarter).
/// Only days with reviews are included; the SPA fills the calendar grid.
/// </summary>
public sealed record GetActivityHeatmapQuery(Guid UserId, int DaysBack = 90)
    : IRequest<Result<IReadOnlyList<HeatmapDayDto>>>;
