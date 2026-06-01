using Memoria.Api.Configuration;
using Memoria.Users.Contracts.Dtos;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Memoria.Api.Endpoints;

internal static class TimeZonesEndpoints
{
    // System timezone catalog doesn't change during the lifetime of the
    // process — compute once on first request and reuse.
    private static readonly Lazy<List<TimeZoneDto>> Catalog = new(BuildCatalog);

    public static IEndpointRouteBuilder MapTimeZonesEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/v1/timezones", () => Catalog.Value)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitingConfiguration.DefaultPolicy);

        return app;
    }

    private static List<TimeZoneDto> BuildCatalog()
    {
        return TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(tz => tz.BaseUtcOffset)
            .Select(tz => new TimeZoneDto(tz.Id, tz.DisplayName))
            .ToList();
    }
}
