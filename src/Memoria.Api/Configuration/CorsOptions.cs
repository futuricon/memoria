using System.ComponentModel.DataAnnotations;

namespace Memoria.Api.Configuration;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    [Required]
    [MinLength(1, ErrorMessage = "Cors:AllowedOrigins must contain at least one origin (no AllowAnyOrigin).")]
    public IReadOnlyList<string> AllowedOrigins { get; init; } = Array.Empty<string>();
}
